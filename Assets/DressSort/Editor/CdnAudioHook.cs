using System.IO;
using UnityEditor;
using UnityEngine;

// 背景音乐不进包。编辑器里用 CdnArt 的原文件，微信里走 InnerAudioContext。
[InitializeOnLoad]
static class CdnAudioHook
{
    const string Dir = "Assets/DressSort/Editor/CdnAudio/";

    static CdnAudioHook()
    {
        DressSort.Sfx.EditorMusic = Load;
    }

    static AudioClip Load(string name)
    {
        string path = Dir + name + ".mp3";
        AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        if (clip == null && File.Exists(path))
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }
        return clip;
    }
}
