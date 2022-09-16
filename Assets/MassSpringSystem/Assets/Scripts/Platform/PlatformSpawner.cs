/** * Copyright 2020 DrSamatha * 
 * This is a 3D modification of the UnityMassSpringSystem, allowing Mass Spring effect of 3D chunks.
 *
 *  Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated  * documentation files (the "Software"), to deal in the Software without restriction, including without  * limitation the rights to use, copy, modify, merge, publish, distribute, sublicense copies of 
 * the Software, and to permit persons to whom the Software is furnished to do so, subject to the following  * conditions: *  * The above copyright notice and this permission notice shall be included in all copies or substantial  * portions of the Software. *  * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT  * LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO  * EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER  * IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE  * USE OR OTHER DEALINGS IN THE SOFTWARE. */



using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PlatformSpawner : MonoBehaviour
{

    public GameObject MassPrefab;
    public GameObject SpringPrefab;
    public int clickedChunkIndex;
    public List<Vector3> lowPillarInitPositions = new List<Vector3>();

    private List<GameObject> Primitives = new List<GameObject>();
    private readonly int sideLength = 96;
    private Vector3[] positions;
    private Dictionary<int, List<GameObject>> allLowPillars = new Dictionary<int, List<GameObject>>();


    void Start()
    {
        BuildFloor();
        Vector2 [] usedPillars = HighPillar();
        BuildLowPillar(usedPillars);
    }

    void FixedUpdate()
    {
        int numPositions = Primitives.Count;
        Vector3 basicPos = lowPillarInitPositions[clickedChunkIndex];      
        Primitives = allLowPillars[clickedChunkIndex];
        for (int i = 0; i < numPositions; ++i)
            Primitives[i].transform.position = basicPos + TranslateToUnityWorldSpace(positions[i]);
    }



    private void BuildFloor()
    {
        for (int i = 0; i < sideLength; i++)
            for (int j = 0; j < sideLength; j++)
                for (int k = 0; k < 3; k++)
                {
                    Vector3 worldPosition = new Vector3(i - sideLength / 2, k - 3, j - sideLength / 2);
                    object plainUnit = Instantiate(MassPrefab, worldPosition, Quaternion.identity);
                    GameObject plainObject = (GameObject)plainUnit;
                    plainObject.tag = "PlainUnit";
                }
    }


    private Vector2[] HighPillar()
    {
        Vector2[] pillarLocations = new Vector2[16];
        for (int i = 0; i < 4; i++)
            for (int j = 0; j < 4; j++)
            {
                pillarLocations[i * 4 + j] = new Vector2(i * 24 + 20, j * 24 + 20);
            }

        foreach (Vector2 p in pillarLocations)
        {
            for (int i = 0; i < 4; i++)
                for (int j = 0; j < 8; j++)
                    for (int k = 0; k < 4; k++)
                    {
                        Vector3 voxPosition = new Vector3(p.x + i - sideLength / 2, j, p.y + k - sideLength / 2);
                        object plainUnit = Instantiate(MassPrefab, voxPosition, Quaternion.identity);
                        GameObject plainObject = (GameObject)plainUnit;
                        plainObject.tag = "PlainUnit";

                    }
        }
        return pillarLocations;
    }


    private void BuildLowPillar(Vector2[] usedPillars)
    {
        // set locations of pillars.
        Vector2[] pillarLocations = new Vector2[64];  
        for ( int i = 0; i <8; i++)
            for (int j= 0; j<8; j++)
            {
                pillarLocations[i * 8 + j] = new Vector2( i * 12 + 8,  j * 12 + 8);
            }


        int pillarIndex = 0;
        foreach ( Vector2 p in pillarLocations)
        {
            List<GameObject> pillarUnits = new List<GameObject>();
            if (!usedPillars.Contains(p)) 
            {
                int count = 0;
                for (int i = 0; i < 4; i++)
                    for (int j = 0; j < 4; j++)
                        for (int k = 0; k < 4; k++)
                        {
                            Vector3 voxPosition = new Vector3(p.x + i - sideLength / 2, j, p.y + k - sideLength / 2);
                            object lowUnit = Instantiate(SpringPrefab, voxPosition, Quaternion.identity);
                            GameObject lowObject = (GameObject)lowUnit;
                            lowObject.name = pillarIndex + "_" + count.ToString();
                            lowObject.tag = "SpringUnit";   
                            pillarUnits.Add(lowObject);
                            count += 1;

                        }
            }
            allLowPillars[pillarIndex] = pillarUnits;
            lowPillarInitPositions.Add(new Vector3(p.x  - sideLength / 2 -8f, 0, p.y  - sideLength / 2 -8f));
            pillarIndex += 1;
        }
    }


    private Vector3 TranslateToUnityWorldSpace(Vector3 gridPosition)
    {
        return new Vector3(gridPosition.x, gridPosition.z, gridPosition.y);
    }


    public void UpdatePositions(Vector3[] p)
    {
        positions = p;
    }

}
