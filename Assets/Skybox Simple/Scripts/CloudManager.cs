﻿using UnityEngine;
using UnityEngine.Rendering;

namespace BoatAttackSkybox
{
    [ExecuteAlways]
    public class CloudManager : MonoBehaviour
    {
        public float scale = 0.1f;
        public Material material;

        private Cloud[] _clouds;
        
        private void OnValidate()
        {
            Init();
        }

        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += CloudAlign;
            Init();
        }

        private void Init()
        {
            transform.localScale = Vector3.one * scale;
            
            _clouds = new Cloud[transform.childCount];

            for (int i = 0; i < _clouds.Length; i++)
            {
                var cloud = new Cloud
                {
                    T = transform.GetChild(i)
                };
                cloud.Matrix = cloud.T.localToWorldMatrix;
                cloud.Mesh = cloud.T.GetComponent<MeshFilter>().sharedMesh;
                cloud.T.GetComponent<Renderer>().enabled = false;
                _clouds[i] = cloud;
            }
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= CloudAlign;
        }

        private void CloudAlign(ScriptableRenderContext context, Camera camera)
        {
            if (camera.cameraType != CameraType.Preview)
            {
                var t = camera.transform;
                var position = t.position;
                position -= position * scale;
                transform.position = position;
                
                Debug.Log($"Rendering {_clouds.Length} clouds for camera:{camera.name}");
                foreach (var cloud in _clouds)
                {
                    Graphics.DrawMesh(cloud.Mesh, cloud.T.localToWorldMatrix, material, 8);
                }
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.DrawWireSphere(Vector3.zero, 750f);
        }

        private class Cloud
        {
            public Transform T;
            public Matrix4x4 Matrix;
            public Mesh Mesh;
        }
    }
}