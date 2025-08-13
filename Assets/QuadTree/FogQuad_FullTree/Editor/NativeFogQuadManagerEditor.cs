#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(NativeFogQuadManager))]
public class NativeFogQuadManagerEditor : Editor
{
	private NativeFogQuadManager comp;
	private NativeFogQuadManager.NodeStatistics lastStats;
	private bool hasStats = false;

	private void OnEnable()
	{
		comp = (NativeFogQuadManager)target;
	}

	public override void OnInspectorGUI()
	{
		serializedObject.Update();

		EditorGUILayout.LabelField("QuadTree 配置", EditorStyles.boldLabel);
		EditorGUILayout.PropertyField(serializedObject.FindProperty("mapSize"));
		EditorGUILayout.PropertyField(serializedObject.FindProperty("minQuadSize"));

		EditorGUILayout.Space();
		EditorGUILayout.LabelField("编辑操作（格子坐标）", EditorStyles.boldLabel);
		EditorGUILayout.PropertyField(serializedObject.FindProperty("minXY"));
		EditorGUILayout.PropertyField(serializedObject.FindProperty("maxXY"));
		EditorGUILayout.PropertyField(serializedObject.FindProperty("editNodeType"));

		EditorGUILayout.Space();
		using (new EditorGUILayout.HorizontalScope())
		{
			if (GUILayout.Button("插入区域"))
			{
				comp.InsertCurrentArea();
				 SceneView.RepaintAll();
			}
			if (GUILayout.Button("删除区域"))
			{
				comp.RemoveCurrentArea();
				SceneView.RepaintAll();
			}
		}

		using (new EditorGUILayout.HorizontalScope())
		{
			if (GUILayout.Button("重建根节点"))
			{
				comp.RecreateQuad();
				hasStats = false;
				SceneView.RepaintAll();
			}
			if (GUILayout.Button("清空全部"))
			{
				comp.ClearAll();
				hasStats = false;
				SceneView.RepaintAll();
			}
		}

		EditorGUILayout.Space();
		if (GUILayout.Button("统计节点"))
		{
			lastStats = comp.ComputeNodeStatistics();
			hasStats = true;
		}

		if (hasStats)
		{
			EditorGUILayout.LabelField("节点统计", EditorStyles.boldLabel);
			EditorGUILayout.LabelField("总节点数", lastStats.totalNodes.ToString());
			EditorGUILayout.LabelField("叶子节点", lastStats.leafNodes.ToString());
			EditorGUILayout.LabelField("内部节点", lastStats.internalNodes.ToString());

			EditorGUILayout.LabelField("FullyUnlock", lastStats.fullyUnlockNodes.ToString());
			EditorGUILayout.LabelField("FullyLocked", lastStats.fullyLockedNodes.ToString());
			EditorGUILayout.LabelField("PartiallyUnlocked", lastStats.partiallyUnlockedNodes.ToString());
		}

		serializedObject.ApplyModifiedProperties();
	}

	private void OnSceneGUI()
	{
		if (comp == null) return;
		var quad = comp.Quad;

		if (quad == null) return;

		// 遍历并绘制所有叶子节点
		int cap = quad.NodeCapacity;
		for (int i = 0; i < cap; i++)
		{
			if (!quad.Exists(i)) continue;
			if (!quad.IsLeaf(i)) continue;
			DrawNodeRect(quad, i);
		}

		DrawEditAABB(comp.minXY, comp.maxXY, new Color(1.0f, 0.0f, 1.0f, 1.0f));
	}

	private void DrawEditAABB(Vector2Int min, Vector2Int max, Color color)
	{
		Vector3 a = new Vector3(min.x, 0, min.y);
		Vector3 b = new Vector3(max.x, 0, min.y);
		Vector3 c = new Vector3(max.x, 0, max.y);
		Vector3 d = new Vector3(min.x, 0, max.y);

		Handles.color = color;
		float dashSize = 0.5f;
		DrawDashedLine(a, b, dashSize);
		DrawDashedLine(b, c, dashSize);
		DrawDashedLine(c, d, dashSize);
		DrawDashedLine(d, a, dashSize);
	}

	private void DrawDashedLine(Vector3 start, Vector3 end, float dashSize)
	{
		Vector3 direction = (end - start).normalized;
		float distance = Vector3.Distance(start, end);
		
		for (float i = 0; i < distance; i += dashSize * 2)
		{
			Vector3 dashStart = start + direction * i;
			Vector3 dashEnd = start + direction * Mathf.Min(i + dashSize, distance);
			Handles.DrawAAPolyLine(4f, dashStart, dashEnd);
		}
	}

	private void DrawNodeRect(NativeFogQuad quad, int nodeIndex)
	{
		Vector2Int min, max;
		quad.GetNodeBounds(nodeIndex, out min, out max);

		Vector3 a = new Vector3(min.x, 0, min.y);
		Vector3 b = new Vector3(max.x, 0, min.y);
		Vector3 c = new Vector3(max.x, 0, max.y);
		Vector3 d = new Vector3(min.x, 0, max.y);

		Color lineColor = GetColorByType((FogNodeType)quad.GetNodeType(nodeIndex));
		
		{
			var fillColor = new Color(lineColor.r, lineColor.g, lineColor.b, 0.3f);
			Handles.color = fillColor;
			Handles.DrawAAConvexPolygon(a, b, c, d);
		}

		Handles.color = lineColor;
		Handles.DrawAAPolyLine(3f, a, b, c, d, a);
	}

	private Color GetColorByType(FogNodeType type)
	{
		switch (type)
		{
			case FogNodeType.FullyUnlock: return new Color(0.2f, 0.8f, 0.2f, 1.0f);
			case FogNodeType.FullyLocked: return new Color(0.8f, 0.2f, 0.2f, 1.0f);
			case FogNodeType.PartiallyUnlocked: return new Color(1.0f, 0.8f, 0.2f, 1.0f);
			default: return new Color(0.5f, 0.5f, 0.5f, 1.0f);
		}
	}
}
#endif 