using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UI
{
    namespace Util
    {
        public class UIUtils
        {
            public static T FindChild<T>(GameObject go, string name = null, bool recursive = false) where T : Object
            {
                if (go == null)
                    return null;

                // 직계 자식들만 검수
                if (recursive == false)
                {
                    for (int i = 0; i < go.transform.childCount; i++)
                    {
                        Transform transform = go.transform.GetChild(i);
                        if (string.IsNullOrEmpty(name) || transform.name == name)
                        {
                            T component = transform.GetComponent<T>();
                            if (component != null)
                                return component;
                        }
                    }
                }
                // 직계 && 증손자 모든 자식들 검수
                else
                {
                    foreach (T component in go.GetComponentsInChildren<T>())
                    {
                        if (string.IsNullOrEmpty(name) || component.name == name)
                            return component;
                    }
                }
                return null;
            }

            // 컴포넌트 없는 빈 깡통일 때만 사용
            public static GameObject FindChild(GameObject go, string name = null, bool recursive = false)
            {
                Transform transform = FindChild<Transform>(go, name, recursive);
                if (transform == null)
                    return null;

                return transform.gameObject;
            }

            public static T GetOrAddComponent<T>(GameObject go) where T : Component
            {
                T component = go.GetComponent<T>();
                if (component == null)
                    component = go.AddComponent<T>();
                return component;
            }
        }
    }

    namespace Define
    {
        public class UIDefine
        {
            public enum UIEvent
            {
                Click,
                Drag,
            }

            public enum MouseEvent
            {
                Press,
                Click,
            }
        }

        public class PlayerInputUI
        {
            public enum ButtonType
            {
                Move = 0,
                Attack,
                SpecialAction,
                Supply,
                WallSet,
                Skill,
                Confirm
            }
        }

        public class ToastData
        {
            public Sprite Icon;
            public string Description;
        }
    }
}
