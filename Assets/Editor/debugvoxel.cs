using UnityEngine;
using System.Collections.Generic;

public class DebugVoxelLine : MonoBehaviour
{
    public int count = 10;
    public float spacing = 1f;

    void Start()
    {
        GenerateVoxelLine();
    }

    void GenerateVoxelLine()
    {
        GameObject go = new GameObject("DebugVoxelLine");
        go.transform.position = Vector3.zero;

        MeshFilter mf = go.AddComponent<MeshFilter>();
        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        MeshCollider mc = go.AddComponent<MeshCollider>();

        mr.material = new Material(Shader.Find("Standard")) { color = Color.red };

        Mesh mesh = new Mesh();
        mf.mesh = mesh;

        List<Vector3> verts = new List<Vector3>();
        List<int> tris = new List<int>();

        Vector3 back = -transform.forward;
        Vector3 origin = transform.position;

        for (int i = 0; i < count; i++)
        {
            Vector3 pos = origin + back * (i * spacing);
            AddCube(pos, verts, tris);
        }

        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();

        mc.sharedMesh = mesh;
    }

    void AddCube(Vector3 pos, List<Vector3> verts, List<int> tris)
    {
        int start = verts.Count;

        // 8 cube vertices
        verts.Add(pos + new Vector3(-0.5f, -0.5f, -0.5f));
        verts.Add(pos + new Vector3( 0.5f, -0.5f, -0.5f));
        verts.Add(pos + new Vector3( 0.5f,  0.5f, -0.5f));
        verts.Add(pos + new Vector3(-0.5f,  0.5f, -0.5f));

        verts.Add(pos + new Vector3(-0.5f, -0.5f,  0.5f));
        verts.Add(pos + new Vector3( 0.5f, -0.5f,  0.5f));
        verts.Add(pos + new Vector3( 0.5f,  0.5f,  0.5f));
        verts.Add(pos + new Vector3(-0.5f,  0.5f,  0.5f));

        // 12 triangles (two per face)
        int[] faceTris = {
            0,2,1, 0,3,2, // back
            4,5,6, 4,6,7, // front
            0,1,5, 0,5,4, // bottom
            2,3,7, 2,7,6, // top
            1,2,6, 1,6,5, // right
            0,4,7, 0,7,3  // left
        };

        for (int i = 0; i < faceTris.Length; i++)
            tris.Add(start + faceTris[i]);
    }
}
