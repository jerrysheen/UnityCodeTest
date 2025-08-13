using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NativeFogQuadManager : MonoBehaviour
{
	[SerializeField] private Vector2Int mapSize = new Vector2Int(400, 400);
	[SerializeField] public Vector2Int minXY = new Vector2Int(15, 15);
	[SerializeField] public Vector2Int maxXY = new Vector2Int(45, 45);
	[SerializeField] public FogNodeType editNodeType = FogNodeType.FullyUnlock;
	[SerializeField] private int minQuadSize = 25;

	private NativeFogQuad quad;

	public NativeFogQuad Quad => quad;

	void Start()
	{
		EnsureQuad();
	}

	private void EnsureQuad()
	{
		if (quad == null)
		{
			// 使用 X 维度作为方形地图大小
			quad = new NativeFogQuad(mapSize.x, minQuadSize);
		}
	}

	public void RecreateQuad()
	{
		ClearAll();
		quad = new NativeFogQuad(mapSize.x, minQuadSize);
	}

	public void InsertCurrentArea()
	{
		EnsureQuad();
		var aabb = new NativeFogQuad.BoundsAABB(minXY, maxXY);
		quad.Insert(aabb, (byte)editNodeType);
	}

	public void RemoveCurrentArea()
	{
		// 原生结构暂未提供区域删除，后续可通过反向合并或重建策略实现
	}

	public void ClearAll()
	{
		if (quad != null)
		{
			quad.Destroy();
			quad = null;
		}
	}

	// 统计信息结构
	public struct NodeStatistics
	{
		public int totalNodes;
		public int leafNodes;
		public int internalNodes;

		public int fullyUnlockNodes;
		public int fullyLockedNodes;
		public int partiallyUnlockedNodes;
	}

	public NodeStatistics ComputeNodeStatistics()
	{
		NodeStatistics stats = new NodeStatistics();
		if (quad == null)
		{
			return stats;
		}

		int cap = quad.NodeCapacity;
		for (int i = 0; i < cap; i++)
		{
			if (!quad.Exists(i)) continue;
			stats.totalNodes++;
			if (quad.IsLeaf(i)) stats.leafNodes++; else stats.internalNodes++;

			byte t = quad.GetNodeType(i);
			switch (t)
			{
				case 1: // FullyUnlock
					stats.fullyUnlockNodes++;
					break;
				case 2: // FullyLocked
					stats.fullyLockedNodes++;
					break;
				case 3: // PartiallyUnlocked
					stats.partiallyUnlockedNodes++;
					break;
			}
		}

		return stats;
	}

	private void OnDestroy()
	{
		if (quad != null)
		{
			quad.Destroy();
		}
	}
}
