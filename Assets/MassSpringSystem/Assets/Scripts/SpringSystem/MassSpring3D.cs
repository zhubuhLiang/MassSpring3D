/**
 * Copyright 2020 DrSamatha
 * 
 * This is a 3D modification of the UnityMassSpringSystem, allowing Mass Spring effect of 3D chunks.
 *
 *  Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated 
 * documentation files (the "Software"), to deal in the Software without restriction, including without 
 * limitation the rights to use, copy, modify, merge, publish, distribute, sublicense copies of 
 * the Software, and to permit persons to whom the Software is furnished to do so, subject to the following 
 * conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all copies or substantial 
 * portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT 
 * LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO 
 * EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER 
 * IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE 
 * USE OR OTHER DEALINGS IN THE SOFTWARE.
 */



/**
 * Copyright 2017 Sean Soraghan
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated 
 * documentation files (the "Software"), to deal in the Software without restriction, including without 
 * limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of 
 * the Software, and to permit persons to whom the Software is furnished to do so, subject to the following 
 * conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all copies or substantial 
 * portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT 
 * LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO 
 * EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER 
 * IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE 
 * USE OR OTHER DEALINGS IN THE SOFTWARE.
 */


using UnityEngine;
using System;
using System.Collections;


//===========================================================================================
// Simple class that holds various property names and attributes for the 
// MassSpringSystem.compute shader.
//===========================================================================================

public static class SpringComputeShaderProperties
{
    public const string PositionBufferName       = "posBuffer";
    public const string VelocityBufferName       = "velBuffer";
    public const string ExternalForcesBufferName = "externalForcesBuffer";
    public const string NeighboursBufferName     = "neighboursBuffer";
    public const string PropertiesBufferName     = "propertiesBuffer";
    public const string DeltaTimeBufferName      = "deltaTimeBuffer";
    public const int    NumProperties            = 4;
    public const string PosKernel                = "CSMainPos";
    public const string VelKernel                = "CSMainVel";
}

//===========================================================================================
// Simple class that holds various property names and attributes for the 
// SpringRenderShader.shader.
//===========================================================================================

public static class MassSpringRenderShaderProperties
{
    public const string PositionsBuffer = "buf_Points";
    public const string DebugBuffer     = "buf_Debug";
    public const string VelocityBuffer  = "buf_Vels";
}

//===========================================================================================
// Summary
//===========================================================================================
/**
 * This class is used to periodically run position and velocity buffers through compute
 * shader kernels that update them according to a mass spring model. It also provides access
 * to properties of the model that can be tweaked from the editor. This class is used to 
 * update these property values in the compute shader as they are changed externally.
 * 
 * The MassSpawner Spawner member variable is used to spawn and update game objects according
 * to the positions on the mass spring grid. Alternatively, points can be rendered using
 * the RenderShader Shader member variable. In order to do this, uncomment the OnPostRender
 * function and comment the Update function and the call to SpawnPrimitives in the 
 * Initialise function.
 * 
 * Various ComputeBuffer variables are used to read and write data to and from the compute 
 * shader (MassSpringComputeShader).
 * 
 * This class can also be used to translate touch and mouse input (from the UITouchHandler) 
 * into forces that are applied to the mass spring system (implemented by the 
 * MassSpringComputeShader). 
 */

public class MassSpring3D : MonoBehaviour
{
    /** The compute shader that implements the mass spring model.
     */
    public ComputeShader MassSpringComputeShader;
    
    /** A Shader that can be used to render the mass points directly
     *  rather than using game objects in the game world.
     */
    public Shader        RenderShader;
    
    /** The mass of individual mass points in the mass spring model.
     *  Increasing this will make the mass points more resistive to
     *  the springs in the model, but will also reduce their velocity.
     */
    [Range(1.0f, 100.0f)] public float Mass            = 1.0f;  
   
    /** The level of damping in the system. Increasing this value
     *  will cause the system to return to a more 'stable' state quicker,
     *  and will reduce the propagation of forces throughout the grid.
     */ 
    [Range(0.1f, 0.999f)] public float Damping         = 0.1f;
    
    /** The stiffness of the spings in the grid. Increasing this will
     *  cause mass points to 'rebound' with higher velocity, and will
     *  also decrease the time taken for the system to return to a
     *  'stable' state.
     */
    [Range(0.1f, 100.0f)] public float SpringStiffness = 10.0f;

    /** The lenght of the springs in the grid. This defines how far
     *  each mass unit is at a resting state.
     */
    [Range(0.1f, 10.0f)]  public float SpringLength    = 1.0f;

    /** The controller of the game object spawner object.
     */
    public PlatformSpawner Spawner;

    /** The controller of the touch and mouse input handler object.
     */
    public CanvasTouchManager UITouchHandler;

    /** This is the force that will be applied from touch and mouse 
     *  input events on the grid.
     */
    [Range(0.0f, 1000.0f)] public float MaxTouchForce = 100.0f;

    /** Various ComputeBuffer variables are used to read and write data to and from the compute 
     *  shader (MassSpringComputeShader). 
     */
    private ComputeBuffer debugBuffer;
    private ComputeBuffer propertiesBuffer;
    // We fill a buffer of grid neigbour positions and send it to the compute buffer on intialisation, such that 
    // we have access to neughbouring positions in our compute kernels. The neighbours buffer is a buffer of Vector2
    // elements, where the x of each element is the neighbour position and the y is whether that position exists within
    // the bounds of the grid.
    private ComputeBuffer neighboursBuffer; 
    private ComputeBuffer deltaTimeBuffer;
    private ComputeBuffer positionBuffer;
    private ComputeBuffer velocityBuffer;
    private ComputeBuffer externalForcesBuffer;

    /** Our compute shader runs the same kernels in parallel on mutliple blocks of our
     *  mass spring grid. These blocks are of dimensions gridUnitSideX by gridUnitSideY,
     *  and there are numThreadsPerGroupX blocks along the x dimension of our grid and
     *  numThreadsPerGroupY along the Y dimension.
     *  
     *  These values MUST be identical to the gX and gY values in the MassSpringCompute compute shader.
     */
    private const int gridUnitSideX       = 4;  //15
    private const int gridUnitSideY       = 4;  //7
    private const int numThreadsPerGroupX = 1;
    private const int numThreadsPerGroupY = 1;
    private const int LayerCount = 4;
    //private float increment = 0.1f;


    /** The resolution of our entire grid, according to the resolution and layout of the individual
     *  blocks processed in parallel by the compute shader.
     */
    private int GridResX;
    private int GridResY;

    /** The total number of mass points (vertices) in the grid.
     */ 
    private int VertCount;

    /** The two kernels in the compute shader for updating the positions and velocities, respectively. 
     */
    private int PosKernel;
    private int VelKernel;

    /** This material can used to render the mass points directly (rather than using game objects).
     *  This material is instantiated using the RenderShader shader.
     */
    private Material RenderMaterial;

    //===========================================================================================
    // Overrides
    //===========================================================================================
    private void Start ()
    {
        Initialise();
    }

    void Update ()
    {
        HandleTouches();
        Dispatch();
        UpdatePrimitivePositions();
    }

    /* This function can be used for graphical debugging puproses,
     * or if you simply want to render the mass points as points rather
     * than maintaining game objects.
    */
    /*
    void OnPostRender ()
    {
        Dispatch ();
        RenderDataPoints ();
    }
   */

    private void OnDisable()
    {
        ReleaseBuffers();
    }

    //===========================================================================================
    // Accessors
    //===========================================================================================
    
    /** Checks if an object is recognised as a spring mass model game object. 
     */
    public static bool IsMassUnit (string objectTag) { return objectTag == "SpringUnit"; }

    /** Get the values of the mass positions from the compute buffer.
     */
    public Vector3[] GetPositions()
    {
        Vector3[] positions = new Vector3[VertCount];
        positionBuffer.GetData (positions);
        return positions;
    }

    /** Helper functions to get grid dimension properties in the world space.
     */
    public float GetWorldGridSideLengthX()
    {
        return GridResX * SpringLength;
    }

    public float GetWorldGridSideLengthY()
    {
        return GridResY * SpringLength;
    }

    //===========================================================================================
    // Construction / Destruction
    //===========================================================================================

    /** Initialise all of the compute buffers that will be used to update and read from the compute shader.
     *  Fill all of these buffers with data in order to construct a resting spring mass grid of the correct dimensions.
     *  Initialise the position and velocity kernel values using their name values from the SpringComputeShaderProperties static class.
     */
    public void CreateBuffers()
    {
        positionBuffer       = new ComputeBuffer (VertCount, sizeof (float) * 3);
        velocityBuffer       = new ComputeBuffer (VertCount, sizeof (float) * 3);
        externalForcesBuffer = new ComputeBuffer (VertCount, sizeof (float) * 3);
        debugBuffer          = new ComputeBuffer (VertCount, sizeof (float) * 3);
        neighboursBuffer     = new ComputeBuffer (VertCount, sizeof (float) * 48); //12 float pairs changed to 24 pairs.
        propertiesBuffer     = new ComputeBuffer (SpringComputeShaderProperties.NumProperties, sizeof (float));
        deltaTimeBuffer      = new ComputeBuffer (1, sizeof (float));
        
        ResetBuffers();

        PosKernel = MassSpringComputeShader.FindKernel (SpringComputeShaderProperties.PosKernel);
        VelKernel = MassSpringComputeShader.FindKernel (SpringComputeShaderProperties.VelKernel);
    }

    /** Fills all of the compute buffers with starting data to construct a resting spring mass grid of the correct dimensions
     *  according to GridResX and GridResY. For each vertex position we also calculate the positions of each of the neighbouring 
     *  vertices so that we can send this to the compute shader.
     */
    public void ResetBuffers()
    {
        Vector3[] positions  = new Vector3[VertCount ];  
        Vector3[] velocities = new Vector3[VertCount ];  
        Vector3[] extForces  = new Vector3[VertCount ];  
        Vector2[] neighbours = new Vector2[VertCount * 24 ];  

        int neighboursArrayIndex = 0;
        int areaCount = VertCount / LayerCount;

        for (int i = 0; i < VertCount; i++)
        {
            float x = ((i % GridResX - GridResX / 2.0f) / GridResX) * GetWorldGridSideLengthX();
            float y = (((i / GridResX) % GridResY - GridResY / 2.0f) / GridResY) * GetWorldGridSideLengthY();
                     

            positions[i] = new Vector3(x + 10, y + 10, i/areaCount);
            velocities[i] = new Vector3(0.0f, 0.0f, 0.0f);
            extForces[i] = new Vector3(0.0f, 0.0f, 0.0f);
                        
            for (int n = 0; n < 24; n++)  
            {
                int nIdx = GetNeighborIndex(i, n);
                float Flag = nIdx >= 0 ? 1.0f : 0.0f;
                neighbours[neighboursArrayIndex] = new Vector2 (nIdx, Flag);
                neighboursArrayIndex++;
            }
        }


        positionBuffer.SetData       (positions);
        velocityBuffer.SetData       (velocities);
        debugBuffer.SetData          (positions);
        externalForcesBuffer.SetData (extForces);
        neighboursBuffer.SetData     (neighbours);
    }

    public void ReleaseBuffers()
    {
        if (positionBuffer != null)
            positionBuffer.Release();
        if (velocityBuffer != null)
            velocityBuffer.Release();
        if (debugBuffer != null)
            debugBuffer.Release();
        if (propertiesBuffer != null)
            propertiesBuffer.Release();
        if (deltaTimeBuffer != null)
            deltaTimeBuffer.Release();
        if (externalForcesBuffer != null)
            externalForcesBuffer.Release();
        if (neighboursBuffer != null)
            neighboursBuffer.Release();
    }

    void CreateMaterialFromRenderShader()
    {
        if (RenderShader != null)
            RenderMaterial = new Material (RenderShader);
        else 
            Debug.Log ("Warning! Attempting to initialise MassSpringSystem without setting the Shader variable.");
    }

    /** Calculate our entire grid resolution and vertex count from the structure of the compute shader.
     *  Create our render material, and initialise and fill our compute buffers. Send the vertex neighbour 
     *  positions to the compute shader (we only need to do this once, whereas the position and velocities
     *  need to be sent continuously). Finally, we get the initial positions from the compute buffer and use
     *  them to spawn our game objects using the Spawner.
     */
    public void Initialise()
    { 
        GridResX  = gridUnitSideX * numThreadsPerGroupX;
        GridResY  = gridUnitSideY * numThreadsPerGroupY;
        VertCount = GridResX * GridResY * LayerCount; 
        CreateMaterialFromRenderShader();
        CreateBuffers();
        MassSpringComputeShader.SetBuffer (VelKernel/*PosKernel*/, SpringComputeShaderProperties.NeighboursBufferName, neighboursBuffer);
        Vector3[] positions  = new Vector3[VertCount ];  
        positionBuffer.GetData (positions);
    }

    //===========================================================================================
    // Touch Input
    //===========================================================================================



    /** Fill and return an array of Vector2 where x = neighbour position and y = neighbour exists in grid, 
     *  including both direct neighbour positions and "bend" positions.
     *  Bend positions are 2 grid spaces away on both x and y axes, and implement
     *  resistance to bending in the mass spring grid.
     *  
     *  Neighbours are listed in 'clockwise' order of direct neighbours followed by clockwise bend neighbour positions:
     *  north, north-east, east, south-east, south, south-west, west, north-west, north-bend, east-bend, south-bend, west-bend. 
     */


    public int GetNeighborIndex(int Idx, int num)
{
        //  return a group of idx's neighbor's indexes,  if no neighbor is in that direction, then return -100 for it.
        // num refer to which neighbour we are looking to from 0 to 23.
        int sideX = GridResX;
        int sideY = GridResY;
        int height = LayerCount;

        Vector3 p = GetPositionFromIndex(Idx);
        Vector3[] nPoses = new Vector3[]
        {
           new Vector3 ( p.x, p.y +1, p.z),
           new Vector3 ( p.x+1, p.y+1 , p.z),
           new Vector3 ( p.x+1, p.y , p.z),
           new Vector3 ( p.x+1, p.y-1 , p.z),
           new Vector3 ( p.x, p.y-1 , p.z),
           new Vector3 ( p.x-1, p.y-1 , p.z),
           new Vector3 ( p.x-1, p.y , p.z),
           new Vector3 ( p.x-1, p.y+1 , p.z),
           new Vector3 ( p.x, p.y+2 , p.z),
           new Vector3 ( p.x+2, p.y , p.z),
           new Vector3 ( p.x, p.y-2 , p.z),
           new Vector3 ( p.x-2, p.y , p.z),
           new Vector3 ( p.x, p.y , p.z +1),
           new Vector3 ( p.x, p.y+1 , p.z +1),
           new Vector3 ( p.x+1, p.y , p.z +1),
           new Vector3 ( p.x, p.y-1 , p.z+1),
           new Vector3 ( p.x-1, p.y , p.z+1),
           new Vector3 ( p.x, p.y , p.z+2),
           new Vector3 ( p.x, p.y , p.z -1),
           new Vector3 ( p.x, p.y+1 , p.z-1),
           new Vector3 ( p.x+1, p.y , p.z-1),
           new Vector3 ( p.x, p.y-1 , p.z-1),
           new Vector3 ( p.x-1, p.y , p.z-1),
           new Vector3 ( p.x, p.y , p.z-2)
        };
        
        Vector3 nPos = nPoses[num];
        int nIndex;
        if (nPos.x < 0 || nPos.x >= sideX || nPos.y < 0 || nPos.y >= sideY || nPos.z < 0 || nPos.z >= height)
            nIndex = -100;
        else
            nIndex = GetIndexFromPosition(nPoses[num], sideX, sideY);
    
    return nIndex;
    }
    

    public Vector3 GetPositionFromIndex(int Idx)
    {
        int x = Idx % GridResX;
        int y = (Idx / GridResX) % GridResY;
        int z = Idx / (GridResX * GridResY);
        return new Vector3 (x, y, z);
    }

    public int GetIndexFromPosition(Vector3 position, int sideX, int sideY)
    {
        return (int)position.x + (int)position.y * sideX + (int)position.z * sideX * sideY;

    }


    /** Fill and return an array of vertex positions that are the direct neighbouring positions of position
     *  'index' in the mass spring grid.
     *  
     *  Neighbours are listed in 'clockwise' order:
     *  north, north-east, east, south-east, south, south-west, west, north-west
     *  
     *  This function does NOT check the index bounds. 
     */
    public int[] GetNeighbours (int index)
    {
        //directions:n, ne, e, se, s, sw, w, nw
        int[] neighbours = new int[8] {index + GridResX, index + GridResX + 1, index + 1, index - GridResX + 1,
                                       index - GridResX, index - GridResX - 1, index - 1, index + GridResX - 1};

        return neighbours;
    }
        

    /** Returns whether a given index position is within the bounds of the grid. Our grid is structured to have rigid, non-moving edges.
     */
    public bool IndexExists (int index)
    {
        return index >= 0 && index < GridResX * GridResY * LayerCount  ;
    }

    /** Applies a given pressure value to a given mass index. This pressure is added to the 
     *  external forces acting on the grid of masses.
     */
    public void ApplyPressureToMass (int index, Vector3 pressure, ref Vector3[] extForces)
    {
        if (IndexExists(index))
        {
            Vector3 adjustPressure = new Vector3(pressure.x, pressure.z, pressure.y) * -1.0f;
            extForces[index] = adjustPressure * MaxTouchForce;
        }
    }

    /** Gets the neighbouring positions of a mass index and applies reduced pressure to them.
     */
    public void ApplyPressureToNeighbours (int index, Vector3 pressure, ref Vector3[] extForces)
    {
        int[] neighbours = new int[6];
        int[] nums = new int[] { 0, 2, 4, 6, 12, 18 };
        int count = 0;
        foreach (int num in nums)
        {
            neighbours[count] = GetNeighborIndex(index, num);
            count += 1;
        }

        foreach (int i in neighbours)
            if(i >=0)
                ApplyPressureToMass (i, pressure * 0.5f, ref extForces);
    }

    /** Takes in an existing touch or mouse event and transforms it into pressure to be applied at
     *  a specific point on the grid.
     */
    public void UITouchInputUpdated(int index,  Vector3 pressure, ref Vector3[] extForces)
    {
        


        if (index < 0 || index > VertCount)
        {
            string[] data = new string[] { "index:", index.ToString(), "VertCount", VertCount.ToString() }; 
            Debug.Log("Warning: Touch or mouse out of bounds " + String.Join(" ", data));
        }

        ApplyPressureToMass (index, pressure, ref extForces);
        ApplyPressureToNeighbours(index, pressure, ref extForces);
    }

    /** Called continuously by the update function to transform input data to grid forces.
     */
    private void HandleTouches()
    {
        Vector3[] extForces = new Vector3[VertCount];
        for (int i = 0; i < VertCount; i++)
            extForces[i] = new Vector3 (0.0f, 0.0f, 0.0f);

        foreach (ArrayList gridTouch in UITouchHandler.GridTouches)
        {
            UITouchInputUpdated((int)gridTouch[0], (Vector3) gridTouch[1], ref extForces);
        }

        externalForcesBuffer.SetData (extForces);

        UITouchHandler.GridTouches.Clear();
    }

    //===========================================================================================
    // Shader Values
    //===========================================================================================

    void SetGridPropertiesAndTime()
    {
        propertiesBuffer.SetData (new float[] {Mass, Damping, SpringStiffness, SpringLength} );
        deltaTimeBuffer.SetData  (new float[] {Time.deltaTime});
    }

    void SetPositionBuffers()
    {
        MassSpringComputeShader.SetBuffer (PosKernel, SpringComputeShaderProperties.DeltaTimeBufferName,      deltaTimeBuffer);
        MassSpringComputeShader.SetBuffer (PosKernel, SpringComputeShaderProperties.PositionBufferName,       positionBuffer);
        MassSpringComputeShader.SetBuffer (PosKernel, SpringComputeShaderProperties.VelocityBufferName,       velocityBuffer);
        MassSpringComputeShader.SetBuffer (PosKernel, SpringComputeShaderProperties.ExternalForcesBufferName, externalForcesBuffer);  
    }

    void SetVelocityBuffers()
    {
        MassSpringComputeShader.SetBuffer (VelKernel, SpringComputeShaderProperties.PropertiesBufferName,     propertiesBuffer);
        MassSpringComputeShader.SetBuffer (VelKernel, SpringComputeShaderProperties.DeltaTimeBufferName,      deltaTimeBuffer);
        MassSpringComputeShader.SetBuffer (VelKernel, SpringComputeShaderProperties.ExternalForcesBufferName, externalForcesBuffer);
        MassSpringComputeShader.SetBuffer (VelKernel, SpringComputeShaderProperties.VelocityBufferName,       velocityBuffer);
        MassSpringComputeShader.SetBuffer (VelKernel, SpringComputeShaderProperties.PositionBufferName,       positionBuffer);
    }

    void UpdatePrimitivePositions()
    {
        Vector3[] positions = new Vector3[VertCount];
        positionBuffer.GetData (positions);

        if (Spawner != null)
            Spawner.UpdatePositions (positions);
    }

    void Dispatch()
    {
        SetGridPropertiesAndTime();
        
        SetVelocityBuffers();
        MassSpringComputeShader.Dispatch  (VelKernel, gridUnitSideX, gridUnitSideY, 1);

        SetPositionBuffers();
        MassSpringComputeShader.Dispatch  (PosKernel, gridUnitSideX, gridUnitSideY, 1);

    }

    
}
