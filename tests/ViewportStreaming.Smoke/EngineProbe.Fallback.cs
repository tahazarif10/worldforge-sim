using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;
using WorldForge.Camera3D;
using WorldForge.Streaming;
using WorldForge.VisualLab;
using WorldSim;

namespace WorldForge.Tests;

public partial class EngineProbe
{
    // Reflection is confined to this CI-only probe; no fault-injection switch
    // or diagnostic mutation API is installed in the playable application.
    private static T Private<T>(VisualTerrain3D terrain, string field) =>
        (T)(typeof(VisualTerrain3D).GetField(field,
            BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(terrain)!);

    private static Dictionary<(int X, int Y), MeshInstance3D> Details(VisualTerrain3D terrain) =>
        Private<Dictionary<(int X, int Y), MeshInstance3D>>(terrain, "_chunks");

    private static List<MeshInstance3D> FallbackNodes(VisualTerrain3D terrain) =>
        terrain.GetChildren().OfType<MeshInstance3D>()
            .Where(n => n.Name.ToString().StartsWith("ViewportFallback_", StringComparison.Ordinal) &&
                n.IsVisibleInTree() && !n.IsQueuedForDeletion()).ToList();

    private static void InspectFallbackMeshes(VisualTerrain3D terrain)
    {
        WorldState world = Private<WorldState>(terrain, "_world");
        foreach (MeshInstance3D node in FallbackNodes(terrain))
        {
            string[] parts = node.Name.ToString().Split('_');
            int cx = int.Parse(parts[1]), cz = int.Parse(parts[2]);
            Require(node.Mesh is ArrayMesh && node.Mesh.GetSurfaceCount()==1, "actual proxy mesh uploaded");
            Require(!Details(terrain).TryGetValue((cx,cz),out MeshInstance3D? detail) ||
                !detail.IsVisibleInTree(), "fallback and detailed terrain do not overlap");
            using var arrays = node.Mesh!.SurfaceGetArrays(0);
            Vector3[] vertices = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
            Vector3[] normals = arrays[(int)Mesh.ArrayType.Normal].AsVector3Array();
            Color[] colors = arrays[(int)Mesh.ArrayType.Color].AsColorArray();
            Require(vertices.Length<=TerrainFallbackMesh.MaximumVertices &&
                normals.Length==vertices.Length && colors.Length==vertices.Length, "actual bounded surface arrays");
            double area = 0;
            var covered = new HashSet<(int,int)>();
            for(int i=0;i<vertices.Length;i+=3)
            {
                if(normals[i].Y<=.0001f) continue;
                Vector3 a=vertices[i],b=vertices[i+1],c=vertices[i+2],mid=(a+b+c)/3f;
                int x=(int)Math.Floor(mid.X),z=(int)Math.Floor(mid.Z);
                Require(world.Contains(x,z) && world.Tile(x,z) is not (Terrain.DeepWater or Terrain.ShallowWater),
                    "actual proxy triangles never bridge water or world bounds");
                float expected=terrain.SurfaceHeightAt(x,z,mid.X-x,mid.Z-z);
                Require(Math.Abs(mid.Y-expected)<.001f, "actual proxy top matches canonical picked surface");
                area+=Math.Abs((b.X-a.X)*(c.Z-a.Z)-(c.X-a.X)*(b.Z-a.Z))*.5;
                covered.Add((x,z));
            }
            int land=0;
            for(int z=cz*16;z<Math.Min(world.Height,(cz+1)*16);z++)
            for(int x=cx*16;x<Math.Min(world.Width,(cx+1)*16);x++)
                if(world.Tile(x,z) is not (Terrain.DeepWater or Terrain.ShallowWater))
                {
                    land++;
                    Require(covered.Contains((x,z)), "every actual land cell has fallback triangles");
                }
            Require(covered.Count==land && Math.Abs(area-land)<.001,
                "actual projected land area has neither gaps nor duplicated tops");
        }
    }

    private static void CheckCoastEdge(VisualTerrain3D terrain, MeshInstance3D node)
    {
        using var arrays=node.Mesh!.SurfaceGetArrays(0);
        Vector3[] vertices=arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
        Vector3[] normals=arrays[(int)Mesh.ArrayType.Normal].AsVector3Array();
        float expected=terrain.SurfaceHeightAt(15,12,0,0);
        bool found=false;
        for(int i=0;i<vertices.Length;i++)
            if(Math.Abs(normals[i].X+1)<.001 && Math.Abs(vertices[i].X-15)<.001 &&
                Math.Abs(vertices[i].Z-12)<.001 && Math.Abs(vertices[i].Y-expected)<.001)
                found=true;
        Require(found, "sculpted coast bank reaches the same canonical edge as its top");
    }

    private static void RunFallbackLifecycle(VisualTerrain3D terrain, Godot.Camera3D camera)
    {
        bool mirror=terrain.TerrainBackendName=="region-mirror";
        var world=new WorldState(33,25,1315511734);
        for(int z=0;z<world.Height;z++)
        for(int x=0;x<world.Width;x++) world.Paint(x,z,0,Terrain.DeepWater);
        terrain.BindWorld(world,mirror ? new RegionMirrorTerrainSource(world,maxResidentRegions:8) :
            new DirectWorldTerrainSource(world));
        Require(terrain.ActiveFallbackChunks==0, "world reset retires previous proxies");
        var rig=new StrategyCameraRig(camera,terrain.HeightAt,33,25,
            terrain.ReliefAt,()=>terrain.SmoothLandEnabled,terrain.SurfaceHeightAt);
        rig.RestoreReviewPose(new CameraReviewPose(16,12,79));
        terrain.UpdateVisibility(camera,0);
        Require(terrain.StreamingDiagnostics.VisibleChunks==6 &&
            terrain.ActiveFallbackChunks==6 && FallbackNodes(terrain).Count==0 &&
            terrain.StreamingDiagnostics.MissingVisibleChunks==0,
            "partial all-water world covered by existing ocean, with no fake land meshes");
        MeshInstance3D water=terrain.GetNode<MeshInstance3D>("AnimatedWater");
        Require(water.MaterialOverride is ShaderMaterial material &&
            material.Shader.ResourcePath=="res://Assets/Shaders/stylized_water.gdshader",
            "original opaque V2 water shader is retained");
        water.Visible=false;
        Require(terrain.StreamingDiagnostics.MissingVisibleChunks==6,
            "missing actual ocean is detected despite resident proxy records");
        water.Visible=true;

        foreach(var p in new[]{(15,12),(16,12),(17,12),(32,24),(0,0)})
            world.Paint(p.Item1,p.Item2,0,Terrain.Mountain);
        terrain.MarkChanged(16,12,32);
        terrain.UpdateVisibility(camera,0);
        Require(terrain.ActiveTerrainChunks==0 && terrain.StreamingDiagnostics.MissingVisibleChunks==0,
            "water-to-land edits refresh proxies while detail builds are paused");
        Require(terrain.SculptElevation(16,12,1,8)>0 &&
            terrain.SculptElevation(32,24,1,-8)>0, "both sculpt directions update proxy source");
        terrain.UpdateVisibility(camera,0);
        InspectFallbackMeshes(terrain);
        CheckCoastEdge(terrain,FallbackNodes(terrain).Single(n=>n.Name.ToString().StartsWith("ViewportFallback_0_0_")));

        terrain.ToggleSmoothSlopes(); InspectFallbackMeshes(terrain);
        terrain.ToggleRichTerrainDetails(); InspectFallbackMeshes(terrain);
        terrain.ToggleRichTerrainDetails();
        terrain.ToggleSmoothSlopes(); InspectFallbackMeshes(terrain);
        Require(terrain.StreamingDiagnostics.MissingVisibleChunks==0,
            "format toggles refresh proxy-only chunks");

        world.Paint(24,20,0,Terrain.Grass);
        terrain.MarkChanged(24,20,0);
        terrain.FlushChanges();
        InspectFallbackMeshes(terrain);
        world.Paint(24,20,0,Terrain.DeepWater);
        terrain.MarkChanged(24,20,0);
        terrain.FlushChanges();
        Require(terrain.StreamingDiagnostics.MissingVisibleChunks==0,
            "land-to-water edit in a proxy-only chunk refreshes surface coverage");

        ChunkResidencyQueue queue=Private<ChunkResidencyQueue>(terrain,"_viewportQueue");
        Require(queue.TryBegin(new ChunkCoord(0,0),out ChunkRequest attempt),"begin staged replacement");
        typeof(VisualTerrain3D).GetMethod("BuildChunk",BindingFlags.Instance|BindingFlags.NonPublic)!
            .Invoke(terrain,new object[]{0,0});
        Require(!Details(terrain)[(0,0)].Visible && terrain.HasViewportSurfaceCoverage(0,0) &&
            !terrain.HasViewportDetailedCoverage(0,0), "uncompleted detail cannot retire or overlap proxy");
        Require(queue.Fail(attempt) && !queue.Complete(attempt), "failed handoff rejects late completion");
        terrain.UpdateVisibility(camera,0);
        Require(terrain.HasViewportSurfaceCoverage(0,0) && !Details(terrain)[(0,0)].Visible,
            "proxy survives failed replacement and zero-budget retry");

        MeshInstance3D proxy=FallbackNodes(terrain).First();
        proxy.Visible=false;
        Require(terrain.StreamingDiagnostics.MissingVisibleChunks>0,
            "hidden mesh cannot masquerade as coverage");
        terrain.UpdateVisibility(camera,0);
        Require(terrain.StreamingDiagnostics.MissingVisibleChunks==0,"missing actual proxy is repaired");
        int passes=0;
        while(terrain.StreamingDiagnostics.PendingChunks>0)
        {
            terrain.UpdateVisibility(camera,2);
            Require(terrain.StreamingDiagnostics.MissingVisibleChunks==0 && ++passes<=8,
                "partial-world handoff drains with coverage");
        }
        Require(terrain.ActiveFallbackChunks==0 && FallbackNodes(terrain).Count==0,
            "all proxies retired after successful detail completion");
        CheckCoastEdge(terrain,Details(terrain)[(0,0)]);

        Details(terrain)[(0,0)].Mesh=null;
        Require(!terrain.HasViewportDetailedCoverage(0,0) &&
            terrain.StreamingDiagnostics.MissingVisibleChunks>0,
            "dictionary entry alone does not count as a ready land mesh");
        terrain.UpdateVisibility(camera,0);
        Require(terrain.StreamingDiagnostics.MissingVisibleChunks==0 &&
            terrain.StreamingDiagnostics.PendingChunks>0,"missing detail receives coverage and a fresh lease");
        InspectFallbackMeshes(terrain);
        terrain.UpdateVisibility(camera,64);
        Require(terrain.ActiveFallbackChunks==0 && terrain.StreamingDiagnostics.MissingVisibleChunks==0,
            "repaired detail replaces fallback");

        long epoch=terrain.StreamingDiagnostics.WorldEpoch;
        terrain.BindWorld(world,mirror ? new RegionMirrorTerrainSource(world,maxResidentRegions:8) :
            new DirectWorldTerrainSource(world));
        Require(terrain.StreamingDiagnostics.WorldEpoch==epoch+1 &&
            terrain.ActiveFallbackChunks==0 && terrain.ActiveTerrainChunks==0, "reset clears all coverage identity");
        terrain.UpdateVisibility(camera,0);
        InspectFallbackMeshes(terrain);
        Require(terrain.StreamingDiagnostics.MissingVisibleChunks==0,
            "new world obtains fresh coverage without detail budget");
        GD.Print("PASS FALLBACK ENGINE: real meshes, coast/sculpt, water-only, failed/late handoff, repair and reset");
    }
}
