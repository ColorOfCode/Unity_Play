// Crest Ocean System

// Copyright 2021 Wave Harmonic Ltd

Shader "Shuimian/Underwater Effect URP"
{
	Properties
	{
		_Diffuse("Scatter Colour Base", Color) = (0.0, 0.0026954073, 0.16981131, 1.0)
		_DiffuseGrazing("Scatter Colour Grazing", Color) = (0.0, 0.003921569, 0.1686274, 1.0)
		_DiffuseShadow("Scatter Colour Shadow", Color) = (0.0, 0.0013477041, 0.084905684, 1.0)
		[Toggle] _SubSurfaceShallowColour("Enable", Float) = 0
		_SubSurfaceShallowCol("Scatter Colour Shallow", Color) = (0.0, 0.003921569, 0.24705884, 1.0)
		_SubSurfaceDepthMax("Scatter Colour Shallow Depth Max", Range(0.01, 50.0)) = 10.0
		_SubSurfaceDepthPower("Scatter Colour Shallow Depth Falloff", Range(0.01, 10.0)) = 2.5
		_SubSurfaceShallowColShadow("Scatter Colour Shallow Shadow", Color) = (0.0, 0.0053968453, 0.17, 1)
		[Toggle] _SubSurfaceScattering("Enable", Float) = 1
		_SubSurfaceColour("SSS Tint", Color) = (0.08850684, 0.497, 0.45615074, 1.0)
		_SubSurfaceBase("SSS Intensity Base", Range(0.0, 4.0)) = 0.0
		_SubSurfaceSun("SSS Intensity Sun", Range(0.0, 10.0)) = 1.7
		_SubSurfaceSunFallOff("SSS Sun Falloff", Range(1.0, 16.0)) = 5.0
		_DepthFogDensity("Depth Fog Density", Vector) = (0.9, 0.3, 0.35, 1.0)
		_RefractionStrength("Refraction Strength", Range(0.0, 2.0)) = 0.5
		[Toggle] _Caustics("Enable", Float) = 1
		[NoScaleOffset] _CausticsTexture("Caustics", 2D) = "black" {}
		_CausticsTextureScale("Caustics Scale", Range(0.0, 25.0)) = 5.0
		_CausticsTextureAverage("Caustics Texture Grey Point", Range(0.0, 1.0)) = 0.07
		_CausticsStrength("Caustics Strength", Range(0.0, 10.0)) = 3.2
		_CausticsFocalDepth("Caustics Focal Depth", Range(0.0, 250.0)) = 2.0
		_CausticsDepthOfField("Caustics Depth of Field", Range(0.01, 1000.0)) = 0.33
		_CausticsDistortionStrength("Caustics Distortion Strength", Range(0.0, 0.25)) = 0.16
		_CausticsDistortionScale("Caustics Distortion Scale", Range(0.01, 50.0)) = 25.0
	}
	HLSLINCLUDE
	#pragma vertex Vert
	#pragma fragment Frag

	// #pragma enable_d3d11_debug_symbols

	#pragma multi_compile _ _MAIN_LIGHT_SHADOWS
	#pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
	#pragma multi_compile _ _SHADOWS_SOFT

	// Use multi_compile because these keywords are copied over from the ocean material. With shader_feature,
	// the keywords would be stripped from builds. Unused shader variants are stripped using a build processor.
	#pragma multi_compile_local __ _SUBSURFACESCATTERING_ON
	#pragma multi_compile_local __ _SUBSURFACESHALLOWCOLOUR_ON
	#pragma multi_compile_local __ _CAUSTICS_ON
	#pragma multi_compile_local __ _SHADOWS_ON
	#pragma multi_compile_local __ _PROJECTION_PERSPECTIVE _PROJECTION_ORTHOGRAPHIC

	#pragma multi_compile_local __ CREST_MENISCUS
	#pragma multi_compile_local __ _DEBUG_VIEW_OCEAN_MASK
	#pragma multi_compile_local __ _DEBUG_VIEW_STENCIL

	#pragma multi_compile _ CREST_FLOATING_ORIGIN

	#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
	#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
	#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

	ENDHLSL

	SubShader
	{
		ZWrite Off

		Pass
		{
			Name "Full Screen"
			Cull Off
			ZTest Always

			HLSLPROGRAM
			// Both "__" and "_FULL_SCREEN_EFFECT" are fullscreen triangles. The latter only denotes an optimisation of
			// whether to skip the horizon calculation.
			#pragma multi_compile_local __ _FULL_SCREEN_EFFECT

			#include "ShuimianUnderwaterEffect.hlsl"
			ENDHLSL
		}

		Pass
		{
			// Only adds fog to the front face and in effect anything behind it.
			Name "Volume: Front Face (2D)"
			Cull Back
			ZTest LEqual

			HLSLPROGRAM
			#define CREST_WATER_VOLUME 1
			#define CREST_WATER_VOLUME_FRONT_FACE 1
			#include "ShuimianUnderwaterEffect.hlsl"
			ENDHLSL
		}

		Pass
		{
			// Only adds fog to the front face and in effect anything behind it.
			Name "Volume: Front Face (3D)"
			Cull Back
			ZTest LEqual

			HLSLPROGRAM
			#define CREST_WATER_VOLUME 1
			#define CREST_WATER_VOLUME_HAS_BACKFACE 1
			#define CREST_WATER_VOLUME_FRONT_FACE 1
			#include "ShuimianUnderwaterEffect.hlsl"
			ENDHLSL
		}

		Pass
		{
			// Only adds fog to the front face and in effect anything behind it.
			Name "Volume: Front Face (Fly-Through)"
			Cull Back
			ZTest LEqual

			Stencil
			{
				// Must match k_StencilValueVolume in:
				// Scripts/Underwater/UnderwaterRenderer.Mask.cs
				Ref 5
				Comp Always
				Pass Replace
				ZFail IncrSat
			}

			HLSLPROGRAM
			#define CREST_WATER_VOLUME 1
			#define CREST_WATER_VOLUME_HAS_BACKFACE 1
			#define CREST_WATER_VOLUME_FRONT_FACE 1
			#include "ShuimianUnderwaterEffect.hlsl"
			ENDHLSL
		}

		Pass
		{
			// Back face will only render if view is within the volume and there is no scene in front. It will only add
			// fog to the back face (and in effect anything behind it). No caustics.
			Name "Volume: Back Face"
			Cull Front
			ZTest LEqual

			Stencil
			{
				// Must match k_StencilValueVolume in:
				// Scripts/Underwater/UnderwaterRenderer.Mask.cs
				Ref 5
				Comp NotEqual
				Pass Replace
				ZFail IncrSat
			}

			HLSLPROGRAM
			#define CREST_WATER_VOLUME 1
			#define CREST_WATER_VOLUME_BACK_FACE 1
			#include "ShuimianUnderwaterEffect.hlsl"
			ENDHLSL
		}

		Pass
		{
			// When inside a volume, this pass will render to the scene within the volume.
			Name "Volume: Scene (Full Screen)"
			Cull Back
			ZTest Always
			Stencil
			{
				// We want to render over the scene that's inside the volume, but not over already fogged areas. It will
				// handle all of the scene within the geometry once the camera is within the volume.
				// 0 = Outside of geometry as neither face passes have touched it.
				// 1 = Only back face z failed which means scene is in front of back face but not front face.
				// 2 = Both front and back face z failed which means outside geometry.
				Ref 1
				Comp Equal
				Pass Replace
			}

			HLSLPROGRAM
			#define CREST_WATER_VOLUME_FULL_SCREEN 1
			#include "ShuimianUnderwaterEffect.hlsl"
			ENDHLSL
		}
	}
}
