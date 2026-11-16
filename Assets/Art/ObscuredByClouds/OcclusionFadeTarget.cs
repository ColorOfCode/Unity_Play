using UnityEngine;
using System;
using System.Collections.Generic;
namespace ObscuredByClouds.Gameplay
{
    [DisallowMultipleComponent, AddComponentMenu("ObscuredByClouds/遮挡透明对象设置")]
    public sealed class OcclusionFadeTarget : MonoBehaviour
    {
        [InspectorName("允许遮挡渐变"), Tooltip("场景摆放和 Terrain 绘制共用此开关；取消勾选或停用本组件均不参与。")]
        public bool allowFade = true;
        [InspectorName("指定渲染器（留空包含全部 LOD）"), Tooltip("留空处理本物体下的 MeshRenderer；有独立配置组件的子模型由子模型自己管理。")]
        public Renderer[] renderers = new Renderer[0];
        [Serializable]
        public sealed class MaterialBinding
        {
            public Material source;
            public Material transitionMaterial;
            public Shader transitionShader;
        }
        [Tooltip("替换材质优先；未指定时克隆原材质并使用渐变 Shader。自动填充时使用原兼容 Shader，保留雪地与昼夜计算。")]
        public MaterialBinding[] materialBindings = new MaterialBinding[0];

        public static OcclusionFadeTarget Owner(Renderer renderer)=>renderer?renderer.GetComponentInParent<OcclusionFadeTarget>(true):null;
        public bool Includes(Renderer renderer)
        {
            if(!renderer||Owner(renderer)!=this)return false;
            if(renderers==null||renderers.Length==0)return true;
            foreach(var item in renderers)if(item==renderer)return true;
            return false;
        }
        public MaterialBinding BindingFor(Material source)
        {if(materialBindings!=null)foreach(var binding in materialBindings)if(binding!=null&&binding.source==source)return binding;return null;}
        public Shader FadeShaderFor(Material source)
        {
            var binding=BindingFor(source);
            return binding!=null&&binding.transitionMaterial?binding.transitionMaterial.shader:
                binding!=null&&binding.transitionShader?binding.transitionShader:source?source.shader:null;
        }
        public bool CanFade(Material source)
        {return source&&source.renderQueue<=2500&&CharacterOcclusionFader.SupportsFadeShader(FadeShaderFor(source));}
        public Material CreateTransitionMaterial(Material source)
        {
            var binding=BindingFor(source);
            var copy=new Material(binding!=null&&binding.transitionMaterial?binding.transitionMaterial:source);
            if(binding==null||!binding.transitionMaterial)copy.shader=FadeShaderFor(source);
            return copy;
        }
        public int ConfigurationSignature()
        {
            unchecked
            {
                int signature=(enabled?17:31)*397+(allowFade?1:0);
                if(renderers!=null)foreach(var renderer in renderers)signature=signature*397+(renderer?renderer.GetInstanceID():0);
                if(materialBindings!=null)foreach(var binding in materialBindings)if(binding!=null)
                {
                    signature=signature*397+(binding.source?binding.source.GetInstanceID():0);
                    signature=signature*397+(binding.source&&binding.source.shader?binding.source.shader.GetInstanceID():0);
                    signature=signature*397+(binding.transitionMaterial?binding.transitionMaterial.GetInstanceID():0);
                    signature=signature*397+(binding.transitionMaterial&&binding.transitionMaterial.shader?binding.transitionMaterial.shader.GetInstanceID():0);
                    signature=signature*397+(binding.transitionShader?binding.transitionShader.GetInstanceID():0);
                }
                return signature;
            }
        }
        [ContextMenu("扫描材质并自动填充渐变 Shader")]
        public void RefreshMaterialBindings()
        {
            var result=new List<MaterialBinding>();var seen=new HashSet<Material>();
            foreach(var renderer in GetComponentsInChildren<MeshRenderer>(true))
            {
                if(!Includes(renderer))continue;
                foreach(var material in renderer.sharedMaterials)
                {
                    if(!material||!seen.Add(material))continue;
                    var binding=BindingFor(material)??new MaterialBinding{source=material};
                    if(!binding.transitionMaterial&&!binding.transitionShader&&CharacterOcclusionFader.Supports(material))binding.transitionShader=material.shader;
                    result.Add(binding);
                }
            }
            materialBindings=result.ToArray();
        }
        void Reset(){RefreshMaterialBindings();}
    }
}
