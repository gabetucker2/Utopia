using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Occluder : MonoBehaviour
{
    public Prime prime;
    private MapGenerator mapGenerator;
    public Transform playerTransform;
    [Range(1, 200)] public int viewingDistance;

    private void Start()
    {
        mapGenerator = prime.mapGenerator;
    }

    private void Update()
    {
        foreach (Chunk chunk in prime.chunks)
        {
            float sqrThisRange = Mathf.Pow(viewingDistance * mapGenerator.hexSize, 2f);
            MeshRenderer meshRenderer = chunk.GO.GetComponent<MeshRenderer>();
            MeshCollider meshCollider = chunk.GO.GetComponent<MeshCollider>();

            if (Vector3.SqrMagnitude(playerTransform.position - chunk.position) > sqrThisRange && meshRenderer.enabled) { meshRenderer.enabled = false; meshCollider.enabled = false; }//assumes player, who is in proximity, will be the only one colliding
            else if (Vector3.SqrMagnitude(playerTransform.position - chunk.position) <= sqrThisRange && !meshRenderer.enabled) { meshRenderer.enabled = true; meshCollider.enabled = true; }
        }
    }
}
