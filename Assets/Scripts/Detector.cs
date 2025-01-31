﻿using Assets.Scripts;
using Assets.Scripts.TextureProviders;
using NN;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using Unity.Barracuda;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.UI;

public class Detector : MonoBehaviour
{
    [Tooltip("File of YOLO model.")]
    [SerializeField]
    protected NNModel ModelFile;

    [Tooltip("RawImage component which will be used to draw results.")]
    [SerializeField]
    protected RawImage ImageUI;

    [Range(0.0f, 1f)]
    [Tooltip("The minimum value of box confidence below which boxes won't be drawn.")]
    [SerializeField]
    protected float MinBoxConfidence = 0.3f;

    [SerializeField]
    protected TextureProviderType.ProviderType textureProviderType;

    [SerializeReference]
    protected TextureProvider textureProvider = null;

    protected NNHandler nn;
    protected Color[] colorArray = new Color[] { Color.red, Color.green, Color.blue, Color.cyan, Color.magenta, Color.yellow };

    YOLOv8 yolo;

    private void OnEnable()
    {
        nn = new NNHandler(ModelFile);
        yolo = new YOLOv8Segmentation(nn);

        textureProvider = GetTextureProvider(nn.model);
        textureProvider.Start();
    }

    private void Update()
    {
        YOLOv8OutputReader.DiscardThreshold = MinBoxConfidence;
        Texture2D texture = GetNextTexture();

        // Run YOLO inference on the texture and obtain bounding boxes
        var boxes = yolo.Run(texture);

        // Draw results (boxes and class labels) on the texture
        DrawResults(boxes, texture);
        ImageUI.texture = texture;
    }

    protected TextureProvider GetTextureProvider(Model model)
    {
        var firstInput = model.inputs[0];
        int height = firstInput.shape[5];  // Adjust based on model input shape
        int width = firstInput.shape[6];   // Adjust based on model input shape

        TextureProvider provider;
        switch (textureProviderType)
        {
            case TextureProviderType.ProviderType.WebCam:
                provider = new WebCamTextureProvider(textureProvider as WebCamTextureProvider, width, height);
                break;

            case TextureProviderType.ProviderType.Video:
                provider = new VideoTextureProvider(textureProvider as VideoTextureProvider, width, height);
                break;
            default:
                throw new InvalidEnumArgumentException();
        }
        return provider;
    }

    protected Texture2D GetNextTexture()
    {
        return textureProvider.GetTexture();
    }

    void OnDisable()
    {
        nn.Dispose();
        textureProvider.Stop();
    }

    private List<Rect> boxList = new List<Rect>();
    public Color labelColor = Color.green;
    private Texture2D _lineTexture;
    protected void DrawResults(IEnumerable<ResultBox> results, Texture2D img)
    {
        boxList.Clear();
        results.ForEach(box => DrawBox(box, img));
    }

    protected virtual void DrawBox(ResultBox box, Texture2D img)
    {
        boxList.Add(box.rect);
        Color boxColor = colorArray[box.bestClassIndex % colorArray.Length];
        int boxWidth = (int)(box.score / MinBoxConfidence);
        TextureTools.DrawRectOutline(img, box.rect, boxColor, boxWidth, rectIsNormalized: false, revertY: true);
    }
        private void Awake()
    {
        _lineTexture = new Texture2D(1, 1);
        _lineTexture.SetPixel(0, 0, Color.white);
        _lineTexture.Apply();
    }

    private void OnGUI() {
        GUI.color = labelColor;

        foreach (var boxElement in boxList)
        {
            GUI.DrawTexture(new Rect(boxElement.x*Screen.width, boxElement.y*Screen.height, boxElement.width*Screen.width, boxElement.height*Screen.height), _lineTexture);
        }
    }

    private void OnValidate()
    {
        Type t = TextureProviderType.GetProviderType(textureProviderType);
        if (textureProvider == null || t != textureProvider.GetType())
        {
            if (nn == null)
                textureProvider = RuntimeHelpers.GetUninitializedObject(t) as TextureProvider;
            else
            {
                textureProvider = GetTextureProvider(nn.model);
                textureProvider.Start();
            }
        }
    }
}

