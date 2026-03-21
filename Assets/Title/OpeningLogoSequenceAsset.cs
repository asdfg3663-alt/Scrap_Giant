using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class OpeningLogoEntry
{
    public Sprite logoSprite;
    [Min(0f)] public float fadeInDuration = 1f;
    [Min(0f)] public float holdDuration = 4f;
    [Min(0f)] public float fadeOutDuration = 1f;
}

[CreateAssetMenu(menuName = "ScrapGiant/Title/Opening Logo Sequence", fileName = "OpeningLogoSequence")]
public class OpeningLogoSequenceAsset : ScriptableObject
{
    public List<OpeningLogoEntry> logos = new();
}
