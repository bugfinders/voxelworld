using UnityEngine;

public class BottomFaceTest : MonoBehaviour
{
    void Start()
    {
        // Position in front of the player
        Vector3 pos = Camera.main.transform.position +
                      Camera.main.transform.forward * 5f;

        // Raise it so the bottom face is visible
        pos.y += 3f;

        GameObject cube = new GameObject("BottomFaceTestCube");

        MeshFilter mf = cube.AddComponent<MeshFilter>();
        MeshRenderer mr = cube.AddComponent<MeshRenderer>();
        MeshCollider mc = cube.AddComponent<MeshCollider>();

        mr.material = new Material(Shader.Find("Standard"));

        Mesh mesh = new Mesh();

        // 8 cube vertices
        Vector3[] v = new Vector3[]
        {
            new Vector3(0,0,0),
            new Vector3(1,0,0),
            new Vector3(1,1,0),
            new Vector3(0,1,0),
            new Vector3(0,0,1),
            new Vector3(1,0,1),
            new Vector3(1,1,1),
            new Vector3(0,1,1)
        };

        // Only bottom face triangles (y-)
        int[] t = new int[]
        {
            0,1,5,
            0,5,4
        };

        mesh.vertices = v;
        mesh.triangles = t;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        mf.sharedMesh = mesh;
        mc.sharedMesh = mesh;

        cube.transform.position = pos;
    }
}