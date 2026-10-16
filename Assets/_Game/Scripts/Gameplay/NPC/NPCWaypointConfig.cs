using System;
using UnityEngine;

namespace Emergence.Gameplay.NPC
{
    [Serializable]
    public struct NPCWaypointConfig
    {
        public Transform Point;
        [Min(0)] public float WaitDuration;
    }
}
