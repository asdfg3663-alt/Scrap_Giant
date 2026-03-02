using System.Collections.Generic;
using UnityEngine;

public enum Side { Up, Down, Left, Right }

public class Module : MonoBehaviour
{
    [Header("Core? (고정/추가불가 등)")]
    public bool isCore = false;

    [Header("This module can attach using these local sides")]
    public List<Side> attachableLocalSides = new() { Side.Up, Side.Down, Side.Left, Side.Right };

    [Header("Attach Points (child transforms)")]
    public Transform apUp;
    public Transform apDown;
    public Transform apLeft;
    public Transform apRight;

    public Transform GetAttachPoint(Side side) => side switch
    {
        Side.Up => apUp,
        Side.Down => apDown,
        Side.Left => apLeft,
        Side.Right => apRight,
        _ => apUp
    };

    public static Vector2 SideToDir(Side side) => side switch
    {
        Side.Up => Vector2.up,
        Side.Down => Vector2.down,
        Side.Left => Vector2.left,
        Side.Right => Vector2.right,
        _ => Vector2.up
    };
}