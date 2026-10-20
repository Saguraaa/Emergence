using UnityEditor;
using UnityEngine;

namespace Emergence.Gameplay.NPC.Editor
{
    [CustomEditor(typeof(NPCController))]
    public sealed class NPCControllerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var controller = (NPCController)target;
            if (!Application.isPlaying || !controller.enabled) return;
            var resistance = controller.Resistance;
            var behavior = controller.Behavior;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Runtime ECS", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Resistance", $"{resistance.Value:F1} / {resistance.InitialValue:F1}");
            EditorGUILayout.LabelField("Behavior", behavior.Value.ToString());
            EditorGUILayout.LabelField("Route", behavior.RoutePhase.ToString());
            EditorGUILayout.LabelField("Target Index", behavior.TargetIndex.ToString());
            EditorGUILayout.LabelField("Wait Remaining", behavior.WaitRemaining.ToString("F2"));
            Repaint();
        }
    }
}
