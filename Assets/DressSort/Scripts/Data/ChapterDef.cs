using System.Collections.Generic;
using UnityEngine;

namespace DressSort
{
    /// <summary>
    /// 一章关卡。ScriptableObject 的类名必须和文件名一致，
    /// 否则 Unity 找不到对应的脚本，生成出来的资产加载会变成空引用。
    /// </summary>
    [CreateAssetMenu(menuName = "叠叠裙/章节", fileName = "Chapter")]
    public class ChapterDef : ScriptableObject
    {
        public string chapterName = "甜心裙";
        public List<LevelDef> levels = new List<LevelDef>();
    }
}
