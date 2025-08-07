using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct InsertedObj
{
    public string name;
    public Vector2 minXY;
    public Vector2 maxXY;
}
public class QuadTree
{
    private List<InsertedObj> _insertedObjs;
    public int Count { get { return _insertedObjs.Count; } }
    private int _depth;
    public int MAXCOUNT = 5;
    private Vector2 _startPos;
    private Vector2 _endPos;
    private float _nodeWith;
    private float _nodeHeight;
    
    private QuadTree _leftTop;
    private QuadTree _rightTop;
    private QuadTree _leftBottom;
    private QuadTree _rightBottom;

    public QuadTree(Vector2 startPos, Vector2 endPos, int depth)
    {
        _startPos = startPos;
        _endPos = endPos;
        _depth = depth;
        _insertedObjs = new List<InsertedObj>();
        _nodeWith = _endPos.x - _startPos.x;
        _nodeHeight = _endPos.y - _startPos.y;
    }
    
    public void Insert(InsertedObj obj)
    {
        if (!IsAABBBound(_startPos, _endPos, obj.minXY, obj.maxXY)) return;
        if (IsOverlapWithMultipleSubQuadTree(obj) || _depth == 0 || MAXCOUNT > Count)
        {
            _insertedObjs.Add(obj);
        }

        SubDivide();
        _leftBottom.Insert(obj);
        _rightBottom.Insert(obj);
        _leftTop.Insert(obj);
        _rightTop.Insert(obj);
    }

    public void SubDivide()
    {
        Vector2 midPos = (_startPos + _endPos) / 2;
        _leftBottom = new QuadTree(_startPos, midPos, _depth - 1);
        _leftTop = new QuadTree(new Vector2(_startPos.x, midPos.y), new Vector2(midPos.x, _endPos.y), _depth - 1);
        _rightBottom = new QuadTree(midPos, _endPos, _depth - 1);
        _rightTop = new QuadTree(new Vector2(midPos.x, _startPos.y), new Vector2(_endPos.x, midPos.y), _depth - 1);
    }

    public void Search(Vector2 min, Vector2 max, ref List<InsertedObj> insertedObjs)
    {
        if (!IsAABBBound(_startPos, _endPos, min, max)) return;
        insertedObjs.AddRange(_insertedObjs);
        if(_leftBottom != null )_leftBottom.Search(min, max, ref insertedObjs);
        if(_rightBottom != null )_rightBottom.Search(min, max, ref insertedObjs);
        if(_leftTop != null )_leftTop.Search(min, max, ref insertedObjs);
        if(_rightTop != null )_rightTop.Search(min, max, ref insertedObjs);
    }

    public bool IsOverlapWithMultipleSubQuadTree(InsertedObj obj)
    {
        int count = 0;
        Vector2 midPos = (_startPos + _endPos) / 2;
        if(IsAABBBound(_startPos, midPos, obj.minXY,obj.maxXY))count++;
        if(IsAABBBound(midPos, _endPos, obj.minXY,obj.maxXY))count++;
        if(IsAABBBound(new Vector2(_startPos.x, midPos.y), new Vector2(midPos.x, _endPos.y), obj.minXY,obj.maxXY))count++;
        if(IsAABBBound(new Vector2(midPos.x, _startPos.y), new Vector2(_endPos.x, midPos.y), obj.minXY,obj.maxXY))count++;
        if(count > 1) return true;
        return false;
    }

    public bool IsAABBBound(Vector2 min, Vector2 max, Vector2 AABBmin, Vector2 AABBmax)
    {
        // 相交碰撞的核心，最大最小比较应该是。
        return min.x < AABBmax.x && max.x > AABBmin.x && min.y < AABBmax.y && max.y > AABBmin.y;
    }
}



