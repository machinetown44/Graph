using System.Collections.Generic;
using UnityEngine;
using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Burst;

using Random = UnityEngine.Random;

public class GraphRenderer : MonoBehaviour
{
    public readonly static int SEGMENTS_ID = Shader.PropertyToID("segments");
    public readonly static int OFFSET_ID = Shader.PropertyToID("offset");
    public readonly static int SEGMENTCOUT_ID = Shader.PropertyToID("segmentCount");

    [SerializeField]
    private int maxSegments = 128;
    [SerializeField]
    private int resolution = 1024;
    [SerializeField]
    private Material mat;

    private List<float2> graphPoints = new List<float2>();
    private Texture2D segments;
    private Texture2D lookup;

    private bool hasChanged = false;
    private float offset = 0;

    public void Graph(float a, float b)
    {
        //store as points for extensibility
        graphPoints.Add(new float2(a, b));
        if (graphPoints.Count > maxSegments + 1)
        {
            graphPoints.RemoveRange(0, graphPoints.Count - maxSegments - 1);
        }

        hasChanged = true;
    }

    private void Awake()
    {
        lookup = new Texture2D(resolution, 1, TextureFormat.RFloat, false);
        lookup.filterMode = FilterMode.Point;
        mat.SetTexture("_MainTex", lookup);
        segments = new Texture2D(maxSegments*4, 1, TextureFormat.RGBAFloat, false);
        segments.filterMode = FilterMode.Point;
        
        mat.SetTexture("_SegmentData", segments);
    }

    private void Update()
    {
        if (hasChanged)
        {
            hasChanged = false;
            UpdateGraph();
        }

        mat.SetFloat(OFFSET_ID, offset);
        mat.SetFloat(SEGMENTCOUT_ID, 1f/(maxSegments * 4));
    }

    private void OnGUI()
    {
        if (GUILayout.Button("Add random value"))
        {
            Graph(Random.value * 0.2f, Random.value);
        }
    }

    private void UpdateGraph()
    {
        UpdateSegments();
        UpdateSliceIDs();
    }
    
    private void UpdateSegments()
    {
        var data = segments.GetRawTextureData<float4>();
        float2 current = float2.zero;
        //convert graph points to line segments for rendering
        //segment data is stored as starting point in x and y, radians of segment direction in z and length in w
        for (int i = 0; i < graphPoints.Count - 1; i++)
        {
            float2 start = current;
            current.x += graphPoints[i].x;
            current.y = graphPoints[i].y;
            float2 direction = current - start;

            float radians = math.asin(math.normalizesafe(direction).y);
            
            float length = math.sqrt(math.dot(direction, direction));

            data[i] = new float4(start.x, start.y, radians, length);
        }

        offset = 1-current.x;
        
        segments.Apply();
    }

    private void UpdateSliceIDs()
    {
        var data = lookup.GetRawTextureData<float>();

        LookupTextureJob job = new LookupTextureJob() { data = data, offset = offset, resolution = resolution, segments = segments.GetRawTextureData<float4>() };

        job.Schedule(data.Length, 128).Complete();

        lookup.Apply();
    }
}

[BurstCompile]
public struct LookupTextureJob : IJobParallelFor
{
    [WriteOnly]
    public NativeArray<float> data;
    public float resolution, offset;
    [ReadOnly]
    public NativeArray<float4> segments;

    public void Execute(int index)
    {
        float currentOffset = (float)index / resolution - offset;

        //map segment indices onto the lookup texture
        for (int j = 0; j < segments.Length; j++)
        {
            float segmentStart = segments[j].x;
            float segmentEnd = segments[j].x + math.cos(segments[j].z) * segments[j].w;

            if (segmentStart < currentOffset && segmentEnd > currentOffset)
            {
                data[index] = j;
                break;
            }
        }
    }
}
