﻿using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine;

namespace Logic
{
    [Flags]
    public enum Direction : byte
    {
        Up    = 0x01,
        Down  = 0x02,
        Left  = 0x04,
        Right = 0x08,
    }

    public static class DirectionUtility
    {
        public static Direction ToDirection(this short numpadDirection, bool reverseInputs = false)
        {
            return ((byte) numpadDirection).ToDirection(reverseInputs);
        }

        public static Direction ToDirection(this byte numpadDirection, bool reverseInputs = false)
        {
            if (numpadDirection < 1 || numpadDirection > 9) throw new ArgumentOutOfRangeException();
            
            var direction = default(Direction);
            var modulus = (numpadDirection - 1) % 3;
            direction |= numpadDirection >= 7 ? Direction.Up : 0;
            direction |= numpadDirection <= 3 ? Direction.Down : 0;
        
            if (!reverseInputs)
            {
                direction |= modulus < 1 ? Direction.Left : 0;
                direction |= modulus > 1 ? Direction.Right : 0;
            }
            else
            {
                direction |= modulus < 1 ? Direction.Right : 0;
                direction |= modulus > 1 ? Direction.Left : 0;
            }
        
            return direction;
        }
    }
    
    [Flags]
    public enum Button : byte
    {
        A     = 0x10,
        B     = 0x20,
        C     = 0x40,
        D     = 0x80,
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct InputState : IEquatable<InputState>
    {
        public byte Inputs;
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool GetButton(Button buttons)
        {
            return (Inputs & (byte) buttons) == (byte) buttons;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool GetDirection(Direction direction)
        {
            return (Inputs & (byte) direction) == (byte) direction;
        }

        public bool Equals(InputState other)
        {
            return Inputs == other.Inputs;
        }

        public static InputState ReadLocalInputs()
        {
            var direction = new int2
            (
                (Input.GetKey(KeyCode.D) ? 1 : 0) + (Input.GetKey(KeyCode.A) ? -1 : 0),
                (Input.GetKey(KeyCode.W) ? 1 : 0) + (Input.GetKey(KeyCode.S) ? -1 : 0)
            );

            return new InputState
            {
                Inputs = (byte) 
                ( 
                    (byte) 
                    (
                        (direction.y > 0 ? Direction.Up : 0)
                      | (direction.y < 0 ? Direction.Down : 0)
                      | (direction.x < 0 ? Direction.Left : 0)
                      | (direction.x > 0 ? Direction.Right : 0) 
                    ) 
                    | (byte) 
                    (
                        (Input.GetKey(KeyCode.U) ? Button.A : 0)
                      | (Input.GetKey(KeyCode.I) ? Button.B : 0) 
                      | (Input.GetKey(KeyCode.O) ? Button.C : 0) 
                      | (Input.GetKey(KeyCode.P) ? Button.D : 0)
                    )
                )
            };
        }
    }
}