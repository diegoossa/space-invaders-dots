using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using System;

namespace Unity.Build
{
    class CustomSearchDropDown<T, T2> : PopupWindowContent
    {
        SearchField m_SearchField;
        ObjectSelectionTreeView<T> m_TreeView;
        [SerializeField]
        TreeViewState m_TreeState;

        Func<IEnumerable<T>> m_objSrcFunc;
        Action<T, T2> m_selFunc;
        Func<T, string, bool> m_filterFunc;
        Func<T, string> m_DisplayFunc;
        T2 m_Context;
        Vector2 m_size;
        Func<Rect, bool> m_footerGUICallback;
        public CustomSearchDropDown(Func<IEnumerable<T>> objSrcFunc, Func<T, string> displayFunc, Func<T, string, bool> filterFunc, Action<T, T2> selFunc, Vector2 size, T2 context, Func<Rect, bool> footerGUICallback = null)
        {
            m_size = size;
            m_DisplayFunc = displayFunc;
            m_objSrcFunc = objSrcFunc;
            m_selFunc = selFunc;
            m_filterFunc = filterFunc;
            m_Context = context;
            m_footerGUICallback = footerGUICallback;
        }

        public override void OnOpen()
        {
            m_SearchField = new SearchField();
            m_SearchField.downOrUpArrowKeyPressed += M_SearchField_downOrUpArrowKeyPressed;
            m_TreeState = new TreeViewState();
            m_TreeView = new ObjectSelectionTreeView<T>(this, m_TreeState, m_objSrcFunc, m_DisplayFunc, m_filterFunc, OnSelection);
            m_TreeView.Reload();
            m_SearchField.SetFocus();
        }

        void OnSelection(T val)
        {
            m_selFunc(val, m_Context);
        }

        private void M_SearchField_downOrUpArrowKeyPressed()
        {
            m_TreeView.SetFocusAndEnsureSelectedItem();
        }
        
        public override void OnGUI(Rect rect)
        {
            var footerHeight = m_footerGUICallback == null ? 0 : EditorGUIUtility.singleLineHeight;
            m_TreeView.searchString = m_SearchField.OnToolbarGUI(new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight), m_TreeView.searchString);
            m_TreeView.OnGUI(new Rect(rect.x, rect.y + EditorGUIUtility.singleLineHeight, rect.width, rect.height - (EditorGUIUtility.singleLineHeight + footerHeight)));
            if (m_footerGUICallback != null && !m_footerGUICallback(new Rect(rect.x, rect.yMax - EditorGUIUtility.singleLineHeight, rect.width, EditorGUIUtility.singleLineHeight)))
                editorWindow.Close();
        }

        public override Vector2 GetWindowSize()
        {
            return m_size;
        }

        class ObjectSelectionTreeView<TVT> : TreeView
        {
            class ObjectTreeViewItem : TreeViewItem
            {
                public TVT userData;
                public ObjectTreeViewItem(TVT o, string displayName, int depth) : base(o == null ? displayName.GetHashCode() : o.GetHashCode(), depth, displayName) { userData = o; }
            }

            Func<IEnumerable<TVT>> m_objSrcFunc;
            Action<TVT> m_selFunc;
            Func<TVT, string, bool> m_filterFunc;
            Func<TVT, string> m_displayFunc;
            PopupWindowContent m_dropDown;
            public ObjectSelectionTreeView(PopupWindowContent dropDown, TreeViewState tvs, Func<IEnumerable<TVT>> objSrcFunc, Func<TVT, string> displayFunc, Func<TVT, string, bool> filterFunc, Action<TVT> selFunc) : base(tvs)
            {
                m_dropDown = dropDown;
                m_displayFunc = displayFunc;
                m_objSrcFunc = objSrcFunc;
                m_selFunc = selFunc;
                m_filterFunc = filterFunc;
            }

            protected override void KeyEvent()
            {
                if (Event.current.Equals(Event.KeyboardEvent("return")))
                {
                    if(state.selectedIDs.Count > 0)
                        UseSelection(state.selectedIDs[0]);
                }
                base.KeyEvent();
            }

            protected override void DoubleClickedItem(int id)
            {
                base.DoubleClickedItem(id);
                UseSelection(id);
            }

            void UseSelection(int id)
            {
                var c = FindItem(id, rootItem);
                m_selFunc((c as ObjectTreeViewItem).userData);
                m_dropDown.editorWindow.Close();
            }

            protected override bool CanMultiSelect(TreeViewItem item)
            {
                return false;
            }

            protected override bool DoesItemMatchSearch(TreeViewItem item, string search)
            {
                return m_filterFunc((item as ObjectTreeViewItem).userData, search.ToLower());
            }

            protected override TreeViewItem BuildRoot()
            {
                TreeViewItem root = new TreeViewItem(-1, -1);
                root.children = new List<TreeViewItem>();
                
                foreach (var o in m_objSrcFunc())
                {
                    var name = m_displayFunc(o);
                    var i = name.IndexOf('/');
                    AddItem(i, name, root, o);
                }
                return root;
            }

            private void AddItem(int i, string name, TreeViewItem parent, TVT o)
            {
                if (i < 0)
                {
                    parent.AddChild(new ObjectTreeViewItem(o, name, parent.depth + 1));
                }
                else
                {
                    TreeViewItem folder = null;
                    var folderName = name.Substring(0, i);
                    if (parent.hasChildren)
                    {
                        foreach (var c in parent.children)
                        {
                            if (c.displayName == folderName)
                            {
                                folder = c;
                                break;
                            }
                        }
                    }
                    if (folder == null)
                    {
                        folder = new ObjectTreeViewItem(default, folderName, parent.depth + 1);
                        parent.AddChild(folder);
                    }
                    var remains = name.Substring(i + 1);
                    AddItem(remains.IndexOf('/'), remains, folder, o);
                }
            }
        }
       
    }
}