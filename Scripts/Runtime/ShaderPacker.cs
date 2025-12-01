using System;
using UnityEngine;


/// <summary>Methods for packing data to pass from the CPU to the GPU efficiently</summary>
public static unsafe class ShaderPacker
{
	#region Methods
	#region Public
	/// <summary>
	/// Packs 2 floats into a single float. 
	/// <b>Only use values between 0 and 1.</b>
	/// <b>There will be a loss in precision.</b>
	/// </summary>
	/// <param name="a">First float.</param>
	/// <param name="b">Second float.</param>
	/// <returns>Packed float with values <paramref name="a"/> and <paramref name="b"/></returns>
	public static float Pack2NormalizedFloats(float a, float b)
	{
		if (a is < 0 or > 1)
			throw new ArgumentOutOfRangeException($"Value of a={a}, must be in the range [0,1]");
		if (b is < 0 or > 1)
			throw new ArgumentOutOfRangeException($"Value of b={b}, must be in the range [0,1]");

		a *= UInt16.MaxValue;
		b *= UInt16.MaxValue;
		uint aInt = (UInt32)Mathf.FloorToInt(a);
		uint bInt = ((UInt32)Mathf.FloorToInt(b)) << 16;
		uint combine = aInt | bInt;

		return UInt32ToSingle(combine);
	}

	/// <summary>
	/// Unpacks a floats into 2 values. 
	/// </summary>
	/// <param name="packed">Packed float.</param>
	/// <returns>A tuple of 2 unpacked float values</returns>
	public static (float, float) Unpack2Floats(float packed)
	{
		uint value = SingleToUInt32(packed);
		uint aInt = value & 0x0000ffff;
		uint bInt = (value & 0xffff0000) >> 16;

		Vector2 result = new Vector2(aInt, bInt) / 0x0000ffff;
		return (result.x, result.y);
	}
	
	public static float PackColor(Color32 c)
	{
		// Clamp alpha to 254 to avoid creating NaN bit patterns
		// Alpha values of 255 with non-zero RGB create IEEE 754 NaN values
		byte clampedAlpha = (byte)Math.Min((int)c.a, 254);
		uint packed = (uint)(c.r | (c.g << 8) | (c.b << 16) | (clampedAlpha << 24));
		return UInt32ToSingle(packed);
	}

	public static Color32 UnpackColor(float c)
	{
		uint packed = SingleToUInt32(c);
		return new Color32(
			(byte)(packed & 0xFF),
			(byte)((packed >> 8) & 0xFF),
			(byte)((packed >> 16) & 0xFF),
			(byte)((packed >> 24) & 0xFF)
		);
	}
	
	#endregion
	
	#region Private
	/// <summary>
	/// Converts <paramref name="value"/> to an unsigned integer.
	/// </summary>
	/// <param name="value">The value to convert.</param>
	/// <returns>The converted value.</returns>
	public static uint SingleToUInt32(float value) => *(uint*)(&value);

	/// <summary>
	/// Converts <paramref name="value"/> to a float.
	/// </summary>
	/// <param name="value">The value to convert.</param>
	/// <returns>The converted value.</returns>
	public static float UInt32ToSingle(uint value) => *(float*)(&value);
	#endregion
	#endregion
}
