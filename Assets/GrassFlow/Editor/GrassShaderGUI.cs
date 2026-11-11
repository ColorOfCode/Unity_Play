using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.AnimatedValues;

using static GrassFlow.ShaderVariantHelper;

using PropType = UnityEditor.MaterialProperty.PropType;
using PropFlags = UnityEditor.MaterialProperty.PropFlags;

namespace GrassFlow {
    public class GrassShaderGUI : ShaderGUI {

        bool m_FirstTimeApply = true;



        static Dictionary<string, AnimBool> foldoutDict;
        static Stack<bool> nestedFoldouts;
        static Stack<string> nestedFoldoutProps;

        static GUIStyle foldoutStyle;

        public override void AssignNewShaderToMaterial(Material material, Shader oldShader, Shader newShader) {
            base.AssignNewShaderToMaterial(material, oldShader, newShader);

            //this is really stupid but by default shaders are created in the standard shader
            //which sets this to 0.5, which we dont want
            material.SetFloat("_Cutoff", 0);
        }


        void CreateStyles() {
            foldoutStyle = new GUIStyle(EditorStyles.foldout) {
                fontStyle = FontStyle.Bold, fontSize = 12
            };
        }

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] props) {

            CreateStyles();

            EditorGUIUtility.fieldWidth = 50;
            EditorGUIUtility.labelWidth = EditorGUIUtility.currentViewWidth * 0.75f;

            Material mat = materialEditor.target as Material;

            if (m_FirstTimeApply) {
                //UpdateMaterial(mat);
                UpdateBools(materialEditor, props);
                m_FirstTimeApply = false;
            }

#if GRASSFLOW_SRP
            if (mat.GetFloat(pipeTypeID) != 1 && PipelineMaterialChecker.CheckURP()) {
                EditorGUILayout.HelpBox("URP detected but material not set to URP.", MessageType.Warning);
            }
#endif
#if GRASSFLOW_HDRP
            if (mat.GetFloat(pipeTypeID) != 2 && PipelineMaterialChecker.CheckHDRP()) {
                EditorGUILayout.HelpBox("HDRP detected but material not set to HDRP.", MessageType.Warning);
            }
#endif

            bool shaderChanged = DrawShaderVariantUI(mat, materialEditor);
            if (shaderChanged) {
                //i don't even know, this makes no fucking sense
                //but if try to return instead of this unity throws a weird ui error that doesnt matter so
                //w/e
                props = new MaterialProperty[0];
            }


            MaterialProperty mainTexProp = null;
            if (props.Length > 0) {
                mainTexProp = FindProperty("_MainTex", props);
            }

            EditorGUILayout.HelpBox("Check the tooltips or documentation for information on material settings", MessageType.Info, true);




            bool hideIf = false;

            int propIdx = -1;
            foreach (MaterialProperty prop in props) {
                propIdx++;

                bool hideProp = prop.flags.HasFlag(PropFlags.HideInInspector);

                if (prop.name == "_IncIndent") {
                    EditorGUI.indentLevel++;
                    continue;
                }

                if (prop.name == "_DecIndent") {
                    EditorGUI.indentLevel--;
                    continue;
                }


                if (prop.name.StartsWith("_EndHideIf")) {
                    hideIf = false;
                }

                if (hideIf) {
                    continue;
                }

                if (prop.name.StartsWith("_CollapseEnd") &&
                    nestedFoldoutProps.Peek() == prop.displayName) {

                    if (prop.name == "_CollapseEnd_Maps" && nestedFoldouts.Peek()) {
                        materialEditor.TextureScaleOffsetProperty(mainTexProp);
                    }

                    EditorGUILayout.EndFadeGroup();
                    //Debug.Log("pop: " + prop.name);
                    nestedFoldouts.Pop();
                    nestedFoldoutProps.Pop();
                    EditorGUI.indentLevel--;
                    GUILayout.Space(4);
                    continue;
                }

                if (nestedFoldouts.Count != 0 && !nestedFoldouts.Peek()) {
                    continue;
                }

                GUIContent content = new GUIContent(prop.displayName);
                string tooltip;
                if (shaderPropTooltipDict.TryGetValue(prop.name, out tooltip)) {
                    content.tooltip = tooltip;
                }


                if (prop.type == PropType.Texture) {

                    var nextProp1 = (propIdx + 1 < props.Length) ? props[propIdx + 1] : null;
                    bool nextPropHidden = (nextProp1 != null) && (nextProp1.type != PropType.Texture) &&
                        nextProp1.flags.HasFlag(PropFlags.HideInInspector);

                    EditorGUI.BeginChangeCheck();
                    if (nextPropHidden) {
                        var nextProp2 = (propIdx + 2 < props.Length) ? props[propIdx + 2] : null;
                        bool nextPropHidden2 = (nextProp2 != null) && (nextProp2.type != PropType.Texture) &&
                            nextProp2.flags.HasFlag(PropFlags.HideInInspector);

                        if (nextPropHidden2) {
                            materialEditor.TexturePropertySingleLine(content,
                                prop, nextProp1, nextProp2);
                        }
                        else {
                            materialEditor.TexturePropertySingleLine(content,
                                prop, nextProp1);
                        }
                    }
                    else {
                        if (!hideProp) {
                            if (prop.flags.HasFlag(PropFlags.NoScaleOffset)) {
                                materialEditor.TexturePropertySingleLine(content, prop);
                            }
                            else {
                                EditorGUILayout.Space();
                                var rect = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight);
                                materialEditor.TexturePropertyMiniThumbnail(rect, prop, content.text, content.tooltip);

                                const float texWidth = 200;
                                rect = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight);
                                rect.x += texWidth;
                                rect.width -= texWidth;
                                materialEditor.TextureScaleOffsetProperty(rect, prop);
                            }
                        }
                    }

                    if (EditorGUI.EndChangeCheck()) {
                        HandleTexFeatureKeyword(mat, prop);
                    }

                    if (prop.name == "_EmissionMap") {
                        //materialEditor.LightmapEmissionProperty(2);
                        MaterialEditor.FixupEmissiveFlag(mat);
                        materialEditor.LightmapEmissionFlagsProperty(2, true);
                        //mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.
                    }

                    continue;
                }



                if (prop.name.StartsWith("_CollapseStart")) {
                    //drawProps = GetPropDefined(prop);

                    //GUILayout.Space(15);
                    EditorGUILayout.BeginHorizontal();

                    AnimBool animBool = CheckFoldoutDict(prop.displayName);
                    if (animBool == null) {
                        UpdateBools(materialEditor, props);
                        animBool = CheckFoldoutDict(prop.displayName);
                    }

                    EditorGUI.BeginChangeCheck();

                    animBool.target = EditorGUILayout.Foldout(
                        animBool.target,
                        prop.displayName,
                        true,
                        foldoutStyle
                    );

                    if (EditorGUI.EndChangeCheck()) {
                        SetPref(prop, animBool.target);
                    }

                    //GUILayout.Label(prop.displayName, EditorStyles.boldLabel);

                    if (!hideProp) {
                        materialEditor.ShaderProperty(prop, content);
                    }


                    EditorGUILayout.EndHorizontal();


                    nestedFoldouts.Push(EditorGUILayout.BeginFadeGroup(animBool.faded));
                    nestedFoldoutProps.Push(prop.displayName);
                    EditorGUI.indentLevel++;
                    //Debug.Log("push: " + prop.name + " : " + nestedFoldouts.Peek().ToString());

                    continue;
                }

                if (prop.name.StartsWith("_HideIf")) {
                    hideIf = !prop.GetPropSetOrEnabled();
                }

                if (prop.name.StartsWith("_Space")) {
                    GUILayout.Space(15);
                    continue;
                }


                if (prop.name.StartsWith("_header")) {
                    DrawHeader(prop.displayName);
                    continue;
                }



                //Just a normal prop

                if (hideProp) {
                    continue;
                }




                switch (prop.type) {

                    case PropType.Texture:
                        materialEditor.TexturePropertySingleLine(content, prop);
                        if (!prop.flags.HasFlag(PropFlags.NoScaleOffset)) {
                            materialEditor.TextureScaleOffsetProperty(prop);
                        }
                        break;


                    default:

                        if (prop.type == PropType.Vector) {
                            GUILayout.Space(5);
                            EditorGUILayout.PrefixLabel(content);
                            content.text = "";
                        }

                        materialEditor.ShaderProperty(prop, content);
                        break;
                }
            }

            GUILayout.Space(10);
            DrawHeader("Other");
            materialEditor.RenderQueueField();
            materialEditor.EnableInstancingField();
            //materialEditor.DoubleSidedGIField();

            wasOpen = true;
        }

        static AnimBool CheckFoldoutDict(string key) {
            if (foldoutDict.ContainsKey(key)) {
                return foldoutDict[key];
            }
            else {
                return null;
            }
        }

        static void UpdateBools(MaterialEditor matEdit, MaterialProperty[] props) {

            foldoutDict = new Dictionary<string, AnimBool>();
            nestedFoldouts = new Stack<bool>();
            nestedFoldoutProps = new Stack<string>();

            foreach (MaterialProperty prop in props) {
                if (prop.name.StartsWith("_CollapseStart")) {

                    AnimBool aBool = new AnimBool(GetPref(prop));
                    aBool.valueChanged.AddListener(matEdit.Repaint);
                    aBool.speed *= 2;
                    foldoutDict.Add(prop.displayName, aBool);
                }
            }


            if (sVariantFoldout == null) {
                sVariantFoldout = new AnimBool(GetPref("ShaderVariants"));
                sVariantFoldout.speed *= 2;
            }
            else sVariantFoldout.valueChanged.RemoveAllListeners();
            sVariantFoldout.valueChanged.AddListener(matEdit.Repaint);
        }



        bool wasOpen = false;
        static AnimBool sVariantFoldout;

        //returns whether or not the shader changed
        bool DrawShaderVariantUI(Material mat, MaterialEditor matEdit) {


            AnimBool aBool = sVariantFoldout;
            bool fadeTarget = aBool.target;
            aBool.target = EditorGUILayout.Foldout(
                aBool.target,
                "Shader Variants",
                true,
                foldoutStyle
            );
            if (fadeTarget != aBool.target) {
                SetPref("ShaderVariants", aBool.target);
            }

            bool forceCompile = false;
            if (!wasOpen) {
                //handle checks that might require to recompile the shader
                if (!mat.HasProperty(VERSIONID)) {
                    ShaderVariantHelper.PortOldShader(mat);
                    return true;
                }
                else {
                    forceCompile = ShaderVariantHelper.CheckShaderNeedsRecompilation(mat);
                }
            }

            if (aBool.faded == 0 && !forceCompile) {
                return false;
            }
            GrassFlowRenderer gf = GrassFlowInspector.currentlyDrawnMesh?.owner;
            return ShaderVariantHelper.HandleVariantGuiAndCompilation(gf, mat, aBool.faded, forceCompile);
        }



        //
        //UTILITY
        //

        const string prefsfix = "GFGUI_";

        static bool GetPref(string name) { return EditorPrefs.GetBool(prefsfix + name, true); }

        static void SetPref(string name, bool val) { EditorPrefs.SetBool(prefsfix + name, val); }

        static bool GetPref(MaterialProperty prop) { return GetPref(prop.displayName); }

        static void SetPref(MaterialProperty prop, bool val) { SetPref(prop.displayName, val); }


        void DrawHeader(string text) {
            GUILayout.Space(10);
            EditorGUILayout.LabelField(text, EditorStyles.boldLabel);
        }



        void HandleTexFeatureKeyword(Material mat, MaterialProperty prop) {
            mat.SetKeyword(prop.name.ToUpper(), prop.GetPropSetOrEnabled());
        }



        static Dictionary<string, string> shaderPropTooltipDict = new Dictionary<string, string>() {
            {"bladeOffset", "Adds a height offset to the position of the grass on the terrain, can be useful for fine tuning."},
            {"bladeSharp", "Controls sharpness of grass blades, 0 is perfect point, 1 is rectangular."},
            {"seekSun", "Controls how much the grass aligns to the surface normal. 0 aligns all the way, 1 points up."},
            {"topViewPush", "Attempts to add a slight offset to the grass when viewed from above which can help to give more depth and density when looking down."},
            {"flatnessMult", "Controls how \"flat\" the grass is pushed when using the flatness channel of the parameter map."},
            {"_BILLBOARD", "Whether or not the grass should always face the camera."},
            {"variance", "These four values control how randomized the grass is in certain ways. The values are: X = Position, Y = Height, Z = Color, W = Width"},

            //Lighting
            {"_ppLights","Calculate shading per pixel. Slightly slower, only really noticeable when using custom grass meshes but is required for normal mapping."},
            {"_AO", "Controls how dark the bottom of the grass blades are, 0 is darker, 1 is no darkness. "},
            {"ambientCO", "Controls how dark the shading can be."},
            {"ambientCOShadow", "On top of the light source shadow strength setting, this allows you to further tune received shadow strength."},
            {"edgeLight", "Controls strength of added brightness when the light direction is edge on to the grass blades."},
            {"edgeLightSharp", "Controls sharpness of the added edge on light brightness."},
            {"blendNormal", "Blends the mesh normals with the terrain surface normal. This allows for better control over shading and specular."},
            {"_GF_SPECULAR", "Enable specular highlights. Adds a small performance cost about 0.1ms in the worst case."},
            {"specSmooth", "Controls smoothness/blurryness of the surface for specular highlights/reflections."},
            {"specularMult", "Multiplier for specular highlight intensity."},
            {"specHeight", "Height adjustment for specular reflections, can be used to tune so that the base of grass doesn't have specular highlights."},
            {"_GF_NORMAL_MAP", "Enable normal mapping. Has a moderate performance cost, about 1ms in the worst case, 0.1ms in a reasonable case."},
            {"normalStrength", "Intensity of normal mapping effect."},
            {"bumpMap", "Texture to use for normal mapping."},

            //Self Shadow
            {"GF_SELF_SHADOW", "Enables a cheap technique to add fake shadows to grass without actually rendering shadows. This works basically by reprojecting your grass texture onto the grass from the perspective of the main light.\n" +
                "Assumes your grass mesh's vertices are -0.5 to 0.5 on the x/z axis, and 0 to 1 on the y axis.\n" +
                "Will look best with a cutout texture and grass cards."},
            {"selfShadowWind", "How much self shadow is modulated by wind to give it motion."},
            {"selfShadowScaleOffset", "(x,y): The scale applied to the shadow projection.\n(z,w): Offset applied to the shadow projection.\nYou can tweak these to fine tune the placement for your particular mesh."},


            //LOD
            {"_ALPHA_TO_MASK", "If enabled, AlphaToMask is turned on in the shader. And the performance of this is quite complicated. Sometimes grass looks better with it enabled and sometimes it doesn't."},
            {"widthLODscale", "Controls how the width of blades grows as distance from camera increases. This helps less grass cover the same area while not being very noticeable."},
            {"_GF_USE_DITHER", "Will dither the grass to further hide LOD transitions within a certain distance to camera, or always in deferred mode. " +
                "Most of the time it looks better with this on, but causes some artifacts that may not be desired." +
                "Leave this off unless you notice particularly bad popping on LOD transitions"},
            {"grassFade", "distance the grass visually fades at. NOTE: This does NOT control lod settings, those must be set separately from the GrassFlow component, this setting is visual only."},
            {"grassFadeSharpness", "Sharpness of the grass fade."},
            {"_LOD_SCALING", "Will vertically scale grass for LOD fade-in."},

            //wind
            {"windMult", "Overall wind strength multiplier."},
            {"windTint", "Color the grass is tinted when the wind affects them strongly, alpha controls strength."},
            {"_noiseScale", "Scale of the noise sampling for wind, Sort of controls wind gust size."},
            {"_noiseSpeed", "How fast the noise scrolls accross the grass to change wind patterns. Sort of acts like wind speed but you'll need to adjust wind strength to match."},
            {"windDir  ", "Direction the wind blows, the size of these values determines strength essentially."},
            {"windDir2", "Same as wind direction but controls secondary wind direction, helps give more variety to the wind instead of always being blown in one direction."},
            
            //bend
            {"_MULTI_SEGMENT", "Adds extra segments to each grass blade, allowing it to bend either from the wind, or from curvature. " +
                "The minimum and maximum number of segments can be changed by adjusting the number at the top of the GrassFlow/Shaders/GrassStructsVars.cginc file. " +
                "Based on the LOD settings the number of grass segments is reduced over distance."},
            {"bladeLateralCurve", "How much natural bend the grass has."},
            {"bladeVerticalCurve", "Sort've pulls the grass down towards the surface."},
            {"bladeStiffness", "Controls how much the grass bends in response to wind/ripples."},

            //maps and texturing
            {"_SEMI_TRANSPARENT", "Enables use of textures with alpha."},
            {"alphaLock", "Discards the alpha from the grass texture itself while still applying alpha clipping. Can be useful if your texture has bad alpha or you just don't want to use it."},
            {"alphaMult", "Multiplier for texture alpha, increasing this can allow you to fine tune your texture's alpha if it isn't sharp enough."},
            {"alphaClip", "Controls how sensitive the clipping of transparent textures is."},
            {"numTextures", "Set this to the number of textures in the type map texture atlas. Only used when using a type map."},
            {"textureAtlasScalingCutoff", "Texture index for the type map at which LOD width scaling is turned off. For example: set it to 3 and scaling would only apply to the first three textures in the atlas. " +
                "Only used when using a type map."},
            {"_SpecMap", "Specular map for deferred rendering."},
            {"_OccMap", "Occlusion map for deferred rendering. "},
            {"_MainTex", "Texture used to detail the grass blades/quads. This is the texture used for alpha clip. Can be a horizontal texture atlas used in combination with the type map, make sure to also set the number of textures property if so."},
            {"colorMap", "Color map for GrassFlow. Usually this is set by the GrassFlowRenderer, don't touch this unless you know what you're doing."},
            {"dhfParamMap", "Parameter map for GrassFlow. Usually this is set by the GrassFlowRenderer, don't touch this unless you know what you're doing."},
            {"typeMap", "Type map for GrassFlow. Usually this is set by the GrassFlowRenderer, don't touch this unless you know what you're doing."},


            //optimization
            {"_Cull", "Culling mode for rendering. You may want it set to 'off' if your mesh has double-sided 'gons. Otherwise most of the time you'll probably just want this on backface culling since it's most efficient."},
            {"MESH_COLORS", "Enables use of custom vertex colors on your mesh to determine sensetivity to wind. The red channel of the color is used."},
            {"MESH_NORMALS", "Enables use of the normals on your mesh, otherwise the normal of the terrain is used. For simple grass cards you likely don't want this enabled anyway."},
            {"MESH_UVS", "Enables use of the UVs on your mesh, used for texturing. You'll almost always want this on, but you may as well turn it off if you don't put a texture on your grass."},
            {"MAP_COLOR", "Enables ability to paint the color map at runtime. Otherwise the color is baked in. Enabling this uses an extra texture sample in the shader. Best to leave off for mobile."},
            {"MAP_PARAM", "Enables ability to paint the param map at runtime. Otherwise the values are baked in. Enabling this uses an extra texture sample in the shader. Best to leave off for mobile."},
            {"MAP_TYPE",  "Enables ability to paint the type map at runtime. Otherwise the values are baked in. Enabling this uses an extra texture sample in the shader. Best to leave off for mobile."},
            
            {"GRASS_RIPPLES",  "Enables ability to receive ripples. This can be expensive particularly on mobile because it requires reading from a buffer, even if you're not using ripples, so best to leave off if you don't need it."},
            {"GRASS_FORCES",  "Allows multiple forces on the grass, this can be expensive particularly on mobile because it requires reading from a buffer. If off, one force can still be applied to the grass which is best used for a main character."},
        };

    }

    static class GrassGUIExtensions {
        public static void SetKeyword(this Material m, string keyword, bool state) {
            if (state)
                m.EnableKeyword(keyword);
            else
                m.DisableKeyword(keyword);
        }
        public static int TryGetInt(this Material m, int id, int defaultValue = 0) {
            int result = defaultValue;
            if (m.HasProperty(id)) {
                result = m.GetInt(id);
            }
            return result;
        }
        public static bool TryGetBool(this Material m, int id) {
            return m.GetFloat(id) == 1;
        }

        public static bool TryGetBool(this Material m, int id, bool defaultValue) {
            bool result = defaultValue;
            if (m.HasProperty(id)) {
                result = m.GetFloat(id) == 1;
            }
            return result;
        }

        public static void TrySetInt(this Material m, int id, int value) {
            if (m.HasProperty(id)) {
                m.SetInt(id, value);
            }
        }
        public static void TrySetBool(this Material m, int id, bool value) {
            if (m.HasProperty(id)) {
                m.SetFloat(id, value ? 1 : 0);
            }
        }

        public static bool GetPropSetOrEnabled(this MaterialProperty prop) {
            switch (prop.type) {

                case PropType.Range:
                case PropType.Float:
                    return prop.floatValue != 0;

                case PropType.Texture:
                    return prop.textureValue;

                case PropType.Vector:
                    return prop.vectorValue != Vector4.zero;

                case PropType.Color:
                    return prop.colorValue != Color.clear;

                default:
                    return true;
            }
        }
    }

}