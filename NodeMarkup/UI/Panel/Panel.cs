﻿using ColossalFramework.UI;
using ModsCommon;
using ModsCommon.UI;
using ModsCommon.Utilities;
using NodeMarkup.Manager;
using NodeMarkup.Tools;
using NodeMarkup.UI.Editors;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;

namespace NodeMarkup.UI.Panel
{
    public class NodeMarkupPanel : ToolPanel<Mod, NodeMarkupTool, NodeMarkupPanel>
    {
        #region PROPERTIES

        public override bool Active
        {
            get => base.Active;
            set
            {
                if (value == Active)
                    return;

                base.Active = value;

                if (value)
                {
                    if (CurrentEditor is Editor editor)
                        editor.Active = true;
                }
                else
                {
                    foreach (var editor in Editors)
                        editor.Active = false;
                }
            }
        }

        private float Width => 550f;

        public Markup Markup { get; private set; }
        private bool NeedRefreshOnVisible { get; set; }

        private PanelHeader Header { get; set; }
        private PanelTabStrip TabStrip { get; set; }
        public List<Editor> Editors { get; } = new List<Editor>();
        public Editor PrevEditor { get; set; }
        public Editor CurrentEditor { get; set; }

        public bool Available
        {
            set
            {
                Header.Available = value;
                TabStrip.SetAvailable(value);
            }
        }
        protected override bool NeedRefresh => base.NeedRefresh && NeedRefreshOnVisible;

        #endregion

        #region BASIC

        public override void Awake()
        {
            SingletonItem<NodeMarkupPanel>.Instance = this;

            atlas = TextureHelper.InGameAtlas;
            backgroundSprite = "MenuPanel2";
            name = "NodeMarkupPanel";

            CreateHeader();
            CreateTabStrip();
            CreateEditors();
            CreateSizeChanger();

            minimumSize = GetSize(400);

            base.Awake();
        }
        public override void Start()
        {
            base.Start();

            SetDefaulSize();
            minimumSize = GetSize(200);
        }
        public override void OnEnable()
        {
            base.OnEnable();
            UpdatePanel();
        }
        private void SetDefaulSize()
        {
            SingletonMod<Mod>.Logger.Debug($"Set default panel size");
            size = GetSize(400);
        }
        private Vector2 GetSize(float additional) => new Vector2(Width, Header.height + TabStrip.height + additional);

        #endregion

        #region COMPONENTS

        private void CreateHeader()
        {
            Header = AddUIComponent<PanelHeader>();
            Header.relativePosition = new Vector2(0, 0);
            Header.Target = parent;
            Header.Init(HeaderHeight);
        }
        private void CreateTabStrip()
        {
            TabStrip = AddUIComponent<PanelTabStrip>();
            TabStrip.relativePosition = new Vector3(0, HeaderHeight);
            TabStrip.SelectedTabChanged += OnSelectedTabChanged;
            TabStrip.SelectedTab = -1;
        }
        private void CreateEditors()
        {
            CreateEditor<PointsEditor>();
            CreateEditor<LinesEditor>();
            CreateEditor<CrosswalksEditor>();
            CreateEditor<FillerEditor>();
            CreateEditor<StyleTemplateEditor>();
            CreateEditor<IntersectionTemplateEditor>();
        }
        private void CreateEditor<EditorType>() where EditorType : Editor
        {
            var editor = AddUIComponent<EditorType>();
            editor.Active = false;
            editor.Init(this);
            TabStrip.AddTab(editor);

            Editors.Add(editor);
        }
        private void CreateSizeChanger() => AddUIComponent<SizeChanger>();

        #endregion

        #region UPDATE

        public void SetMarkup(Markup markup)
        {
            if ((Markup = markup) != null)
            {
                if (isVisible)
                    RefreshPanel();
                else
                    NeedRefreshOnVisible = true;
            }
        }
        public void UpdatePanel()
        {
            Available = true;
            foreach (var editor in Editors)
                editor.UpdateEditor();
        }
        public override void RefreshPanel()
        {
            NeedRefreshOnVisible = false;

            Header.Text = Markup.PanelCaption;
            Header.Init(Markup.Type);
            TabStrip.SetVisible(Markup);
            TabStrip.ArrangeTabs();
            TabStrip.SelectedTab = -1;
            SelectEditor<LinesEditor>();
        }
        public void RefreshHeader() => Header.Refresh();

        #endregion

        #region ONEVENTS

        protected override void OnSizeChanged()
        {
            if (Header != null)
                Header.width = width;
            if (TabStrip != null)
                TabStrip.width = width;
            if (CurrentEditor != null)
                SetEditorSize(CurrentEditor);

            base.OnSizeChanged();

            MakePixelPerfect();
        }
        private void SetEditorSize(Editor editor)
        {
            var position = new Vector2(0, TabStrip.relativePosition.y + TabStrip.height);
            editor.relativePosition = position;
            editor.size = size - position;
        }
        private void OnSelectedTabChanged(int index)
        {
            PrevEditor = CurrentEditor;
            CurrentEditor = SelectEditor(index);
        }

        #endregion

        #region GET SELECT

        private Editor SelectEditor(int index)
        {
            if (index >= 0 && Editors.Count > index)
            {
                foreach (var editor in Editors)
                    editor.Active = false;

                var selectEditor = Editors[index];
                selectEditor.Active = true;
                SetEditorSize(selectEditor);
                return selectEditor;
            }
            else
                return null;
        }
        private EditorType SelectEditor<EditorType>() where EditorType : Editor
        {
            var editorIndex = Editors.FindIndex((e) => e.GetType() == typeof(EditorType));
            TabStrip.SelectedTab = editorIndex;
            return Editors[editorIndex] as EditorType;
        }
        public void SelectPrevEditor()
        {
            if(PrevEditor is Editor editor)
            {
                var editorIndex = Editors.IndexOf(editor);
                TabStrip.SelectedTab = editorIndex;
            }
        }

        #endregion

        #region ADD OBJECT

        private void AddObject<EditorType, ItemType>(ItemType item)
            where EditorType : Editor, IEditor<ItemType>
            where ItemType : class, ISupport, IDeletable
        {
            if (Editors.Find(e => e.GetType() == typeof(EditorType)) is EditorType editor)
            {
                editor.Add(item);
                CurrentEditor?.RefreshEditor();
            }
        }
        public void AddLine(MarkupLine line) => AddObject<LinesEditor, MarkupLine>(line);

        #endregion

        #region DELETE OBJECT

        private void DeleteObject<EditorType, ItemType>(ItemType item)
            where EditorType : Editor, IEditor<ItemType>
            where ItemType : class, ISupport, IDeletable
        {
            if (Editors.Find(e => e.GetType() == typeof(EditorType)) is EditorType editor)
            {
                editor.Delete(item);
                CurrentEditor?.RefreshEditor();
            }
        }

        public void DeleteLine(MarkupLine line) => DeleteObject<LinesEditor, MarkupLine>(line);
        public void DeleteCrosswalk(MarkupCrosswalk crosswalk) => DeleteObject<CrosswalksEditor, MarkupCrosswalk>(crosswalk);
        public void DeleteFiller(MarkupFiller filler) => DeleteObject<FillerEditor, MarkupFiller>(filler);

        #endregion

        #region EDIT OBJECT

        private EditorType SelectObject<EditorType, ItemType>(ItemType item)
            where EditorType : Editor, IEditor<ItemType>
            where ItemType : class, ISupport, IDeletable
        {
            if (Markup is not ISupport<ItemType>)
                return null;

            Available = true;
            var editor = SelectEditor<EditorType>();
            editor?.Edit(item);
            return editor;
        }
        public void SelectPoint(MarkupEnterPoint point) => SelectObject<PointsEditor, MarkupEnterPoint>(point);
        public void SelectLine(MarkupLine line) => SelectObject<LinesEditor, MarkupLine>(line);
        public void SelectCrosswalk(MarkupCrosswalk crosswalk) => SelectObject<CrosswalksEditor, MarkupCrosswalk>(crosswalk);
        public void EditCrosswalk(MarkupCrosswalk crosswalk)
        {
            var editor = SelectObject<CrosswalksEditor, MarkupCrosswalk>(crosswalk);
            editor?.BorderSetup();
        }
        public void SelectFiller(MarkupFiller filler) => SelectObject<FillerEditor, MarkupFiller>(filler);

        private void EditTemplate<EditorType, TemplateType>(TemplateType template, bool editName)
            where EditorType : Editor, IEditor<TemplateType>, ITemplateEditor<TemplateType>
            where TemplateType : Template
        {
            var editor = SelectObject<EditorType, TemplateType>(template);
            if (editName && editor != null)
                editor.EditName();
        }

        public void EditStyleTemplate(StyleTemplate template, bool editName = true) => EditTemplate<StyleTemplateEditor, StyleTemplate>(template, editName);
        public void EditIntersectionTemplate(IntersectionTemplate template, bool editName = true) => EditTemplate<IntersectionTemplateEditor, IntersectionTemplate>(template, editName);

        #endregion

        #region ADDITIONAL

        public bool OnEscape() => CurrentEditor?.OnEscape() == true;

        public void Render(RenderManager.CameraInfo cameraInfo) => CurrentEditor?.Render(cameraInfo);

        #endregion
    }
}
