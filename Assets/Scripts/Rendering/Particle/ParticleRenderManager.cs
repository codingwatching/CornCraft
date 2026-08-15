using System;
using System.Collections.Generic;
using UnityEngine;

using CraftSharp.Event;
using CraftSharp.Resource;

namespace CraftSharp.Rendering
{
    public class ParticleRenderManager : MonoBehaviour, IEventListener
    {
        private readonly Dictionary<ParticleExtraDataType, IParticleRender> particleRenders = new();

        private bool initialized = false;

        #nullable enable

        private Action<ParticlesEvent>? particlesCallback;
        private GameObject? blockParticleRenderObject;

        #nullable disable

        private void EnsureInitialized()
        {
            if (initialized || !ResourcePackManager.Instance.Loaded) return;

            initialized = true;

            foreach (var render in particleRenders.Values)
            {
                render.Initialize();
            }
        }

        private void Start()
        {
            ResetRenderer();

            particlesCallback = (e) =>
            {
                if (!initialized || !ResourcePackManager.Instance.Loaded) return;

                var particleType = ParticleTypePalette.INSTANCE.GetByNumId(e.TypeNumId);

                if (particleRenders.TryGetValue(particleType.ExtraDataType, out IParticleRender render))
                {
                    render.AddParticles(e.Position, e.TypeNumId, e.ExtraData, e.Count);
                }
            };

            RebindEventListeners();
        }

        private void OnEnable()
        {
            if (particlesCallback is not null)
            {
                ResetRenderer();
                RebindEventListeners();
            }
        }

        private void ResetRenderer()
        {
            initialized = false;
            particleRenders.Clear();

            if (blockParticleRenderObject)
            {
                if (Application.isPlaying)
                    Destroy(blockParticleRenderObject);
                else
                    DestroyImmediate(blockParticleRenderObject);
            }

            blockParticleRenderObject = new GameObject("Block Particle Render");
            blockParticleRenderObject.transform.SetParent(transform, false);
            particleRenders[ParticleExtraDataType.Block] = blockParticleRenderObject.AddComponent<BlockParticleRender>();
        }

        public void RebindEventListeners()
        {
            if (particlesCallback is not null)
                EventManager.Instance.Register(particlesCallback);
        }

        private void Update()
        {
            EnsureInitialized();

            if (!initialized) return;

            foreach (var render in particleRenders.Values)
            {
                render.ManagedUpdate();
            }
        }

        private void OnDestroy()
        {
            if (particlesCallback is not null)
                EventManager.Instance.Unregister(particlesCallback);
        }
    }
}
