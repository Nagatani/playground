using System.Collections.Generic;
using UnityEngine;

namespace WalkerSim
{
    /// <summary>
    /// 訓練エリア内に凹凸地形（ブロック群）をプロシージャルに生成・更新するスクリプト。
    /// </summary>
    public class TerrainGenerator : MonoBehaviour
    {
        [Header("Terrain Dimensions")]
        [Tooltip("X軸方向（幅）のブロック数")]
        [SerializeField] private int gridWidth = 14;

        [Tooltip("Z軸方向（長さ/進行方向）のブロック数")]
        [SerializeField] private int gridLength = 24;

        [Tooltip("ブロック1つの基本サイズ (X, Z)")]
        [SerializeField] private float blockSize = 1.2f;

        [Tooltip("ブロックの基準の高さ")]
        [SerializeField] private float baseHeight = 2.0f;

        [Header("Roughness Settings")]
        [Tooltip("現在の凹凸度合（0.0: 完全平坦 〜 1.0: 激しい凹凸）")]
        [Range(0f, 2f)]
        public float roughness = 0.0f;

        [Tooltip("roughness=1.0 時の最大高低差")]
        [SerializeField] private float maxHeightVariation = 0.6f;

        [Tooltip("roughness=1.0 時の最大ブロック傾斜角（度）")]
        [SerializeField] private float maxTiltAngle = 15.0f;

        [Tooltip("エージェント初期スポーン位置周辺の平坦化半径")]
        [SerializeField] private float spawnSafeRadius = 2.0f;

        [Header("Layer & Material")]
        [Tooltip("ブロックに付与するレイヤー名")]
        [SerializeField] private string groundLayerName = "Ground";

        [Tooltip("ブロック用マテリアル（省略時はデフォルト）")]
        [SerializeField] private Material blockMaterial;

        [Tooltip("ブロックの物理マテリアル（高摩擦設定）")]
        [SerializeField] private PhysicsMaterial groundPhysicsMaterial;

        private GameObject[] blockPool;
        private Vector3[] basePositions;
        private bool isInitialized = false;

        private void Awake()
        {
            InitializePool();
        }

        public void InitializePool()
        {
            if (isInitialized && blockPool != null && blockPool.Length > 0) return;

            int totalBlocks = gridWidth * gridLength;
            blockPool = new GameObject[totalBlocks];
            basePositions = new Vector3[totalBlocks];

            float startX = -(gridWidth * blockSize) * 0.5f + blockSize * 0.5f;
            float startZ = -(gridLength * blockSize) * 0.5f + blockSize * 0.5f;

            int layer = LayerMask.NameToLayer(groundLayerName);
            if (layer == -1)
            {
                layer = 0;
            }

            int index = 0;
            for (int z = 0; z < gridLength; z++)
            {
                for (int x = 0; x < gridWidth; x++)
                {
                    GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    block.name = $"TerrainBlock_{x}_{z}";
                    block.transform.SetParent(transform, false);

                    block.layer = layer;

                    if (blockMaterial != null)
                    {
                        var renderer = block.GetComponent<Renderer>();
                        if (renderer != null) renderer.sharedMaterial = blockMaterial;
                    }

                    if (groundPhysicsMaterial == null)
                    {
                        groundPhysicsMaterial = new PhysicsMaterial("TerrainFriction")
                        {
                            dynamicFriction = 1.0f,
                            staticFriction = 1.0f,
                            frictionCombine = PhysicsMaterialCombine.Maximum
                        };
                    }

                    var collider = block.GetComponent<BoxCollider>();
                    if (collider != null) collider.sharedMaterial = groundPhysicsMaterial;

                    Vector3 basePos = new Vector3(startX + x * blockSize, 0f, startZ + z * blockSize);
                    block.transform.localPosition = basePos;
                    block.transform.localScale = new Vector3(blockSize * 0.98f, baseHeight, blockSize * 0.98f);

                    basePositions[index] = basePos;
                    blockPool[index] = block;
                    index++;
                }
            }

            isInitialized = true;
        }

        public void RebuildTerrain()
        {
            RebuildTerrain(roughness);
        }

        public void RebuildTerrain(float newRoughness)
        {
            roughness = Mathf.Max(0f, newRoughness);

            if (!isInitialized)
            {
                InitializePool();
            }

            float seedX = Random.Range(0f, 1000f);
            float seedZ = Random.Range(0f, 1000f);

            for (int i = 0; i < blockPool.Length; i++)
            {
                Vector3 basePos = basePositions[i];
                float distFromCenter = Mathf.Sqrt(basePos.x * basePos.x + basePos.z * basePos.z);

                float safeFactor = Mathf.Clamp01((distFromCenter - spawnSafeRadius * 0.5f) / spawnSafeRadius);
                float currentRough = roughness * safeFactor;

                float noise = Mathf.PerlinNoise(seedX + basePos.x * 0.3f, seedZ + basePos.z * 0.3f) - 0.5f;
                float randomOffset = (Random.value - 0.5f) * 0.4f;
                float heightOffset = (noise * 1.6f + randomOffset) * maxHeightVariation * currentRough;

                Vector3 targetPos = basePos;
                targetPos.y = heightOffset - (baseHeight * 0.5f);
                blockPool[i].transform.localPosition = targetPos;

                if (currentRough > 0.05f)
                {
                    float tiltX = (Random.value - 0.5f) * 2f * maxTiltAngle * currentRough;
                    float tiltZ = (Random.value - 0.5f) * 2f * maxTiltAngle * currentRough;
                    blockPool[i].transform.localRotation = Quaternion.Euler(tiltX, 0f, tiltZ);
                }
                else
                {
                    blockPool[i].transform.localRotation = Quaternion.identity;
                }
            }
        }

        private void OnDestroy()
        {
            if (blockPool != null)
            {
                for (int i = 0; i < blockPool.Length; i++)
                {
                    if (blockPool[i] != null)
                    {
                        Destroy(blockPool[i]);
                    }
                }
            }
        }
    }
}
