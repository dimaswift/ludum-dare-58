using System;
using UnityEngine;

namespace Uriel.Behaviours
{
    [CreateAssetMenu(menuName = "Glasma/Marching Cubes")]
    public class MarchingCubesConfig : ScriptableObject
    {
        public int budget;
        public bool flipNormals;
        public bool invertTriangles;
    }
}