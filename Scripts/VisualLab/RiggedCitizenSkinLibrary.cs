using System;
using System.Collections.Generic;
using Godot;

namespace WorldForge.VisualLab;

/// <summary>
/// Shares one actual skinned mesh per clothing palette between all citizens.
/// Every vertex receives one rigid skeleton bind matching the original
/// bone-attachment artwork (no loss of the approved low-poly silhouette).
/// A villager therefore needs only one MeshInstance3D draw, not twelve.
/// </summary>
internal static class RiggedCitizenSkinLibrary
{
    internal sealed record Art(ArrayMesh Mesh,Skin Skin,int VertexCount);
    private static readonly Art?[] Cache=new Art?[3];

    private readonly record struct Piece(int Bone,Mesh Mesh,Vector3 AttachmentOffset);

    internal static Art Get(int palette,Skeleton3D skeleton)
    {
        if(Cache[palette] is not null)return Cache[palette]!;
        var colors=CitizenArt3D.Palettes[palette];
        var material=new StandardMaterial3D
        {
            VertexColorUseAsAlbedo=true,
            Roughness=.93f,Metallic=0f,
            CullMode=BaseMaterial3D.CullModeEnum.Disabled
        };

        // Same authored meshes used by the approved single-person
        // Skeleton3D prototype. Palette and rest offsets are unchanged.
        Mesh upper=CitizenArt3D.BuildRigUpperArm(colors,material);
        Mesh fore=CitizenArt3D.BuildRigForearm(colors,material);
        Mesh thigh=CitizenArt3D.BuildRigThigh(colors,material);
        Mesh shin=CitizenArt3D.BuildRigShin(colors,material);
        Mesh foot=CitizenArt3D.BuildRigFoot(colors,material);
        Piece[] pieces=
        [
            new(1,CitizenArt3D.BuildRigTorso(colors,material),
                new Vector3(0,-.80f,0)),
            new(2,CitizenArt3D.BuildRigHead(colors,material),
                new Vector3(0,-1.13f,0)),
            new(3,upper,Vector3.Zero),new(4,fore,Vector3.Zero),
            new(5,upper,Vector3.Zero),new(6,fore,Vector3.Zero),
            new(7,thigh,Vector3.Zero),new(8,shin,Vector3.Zero),
            new(9,foot,Vector3.Zero),new(10,thigh,Vector3.Zero),
            new(11,shin,Vector3.Zero),new(12,foot,Vector3.Zero)
        ];

        var positions=new List<Vector3>();
        var normals=new List<Vector3>();
        var shades=new List<Color>();
        var bones=new List<int>();
        var weights=new List<float>();

        foreach(Piece piece in pieces)
        {
            if(piece.Mesh.GetSurfaceCount()!=1)
                throw new InvalidOperationException("Citizen piece must have one surface.");
            using var data=piece.Mesh.SurfaceGetArrays(0);
            Vector3[] vertex=data[(int)Mesh.ArrayType.Vertex].AsVector3Array();
            Vector3[] normal=data[(int)Mesh.ArrayType.Normal].AsVector3Array();
            Color[] color=data[(int)Mesh.ArrayType.Color].AsColorArray();
            if(vertex.Length!=normal.Length || vertex.Length!=color.Length)
                throw new InvalidOperationException("Citizen mesh attributes are misaligned.");

            // Mesh source coordinates are local to a named attachment bone.
            // Convert them into the skeleton's world-space REST position;
            // Skin's inverse bind will move each piece with that same bone.
            Transform3D rest=skeleton.GetBoneGlobalRest(piece.Bone);
            for(int i=0;i<vertex.Length;i++)
            {
                positions.Add(rest*(vertex[i]+piece.AttachmentOffset));
                normals.Add((rest.Basis*normal[i]).Normalized());
                shades.Add(color[i]);
                bones.Add(piece.Bone);bones.Add(0);bones.Add(0);bones.Add(0);
                weights.Add(1f);weights.Add(0f);weights.Add(0f);weights.Add(0f);
            }
        }

        using var arrays=new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex]=positions.ToArray();
        arrays[(int)Mesh.ArrayType.Normal]=normals.ToArray();
        arrays[(int)Mesh.ArrayType.Color]=shades.ToArray();
        arrays[(int)Mesh.ArrayType.Bones]=bones.ToArray();
        arrays[(int)Mesh.ArrayType.Weights]=weights.ToArray();

        var mesh=new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles,arrays);
        if(mesh.GetSurfaceCount()!=1)
            throw new InvalidOperationException("Skinned citizen mesh upload failed.");
        mesh.SurfaceSetMaterial(0,material);

        var skin=new Skin();
        skin.SetBindCount(skeleton.GetBoneCount());
        for(int i=0;i<skeleton.GetBoneCount();i++)
        {
            skin.SetBindBone(i,i);
            skin.SetBindPose(i,skeleton.GetBoneGlobalRest(i).AffineInverse());
        }
        var art=new Art(mesh,skin,positions.Count);
        Cache[palette]=art;
        GD.Print($"Rigged citizen palette {palette}: one skinned mesh, {positions.Count} vertices, {skin.BindCount} bones");
        return art;
    }
}
