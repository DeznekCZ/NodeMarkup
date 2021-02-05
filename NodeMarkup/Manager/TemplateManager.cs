﻿using ColossalFramework;
using ColossalFramework.Importers;
using ColossalFramework.IO;
using ColossalFramework.Packaging;
using ColossalFramework.PlatformServices;
using ModsCommon.Utilities;
using NodeMarkup.Utils;
using System;
using System.CodeDom;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using UnityEngine;

namespace NodeMarkup.Manager
{
    public abstract class TemplateManager
    {
        public static StyleTemplateManager StyleManager
        {
            get => StyleTemplateManager.Instance;
            private set => StyleTemplateManager.Instance = value;
        }
        public static IntersectionTemplateManager IntersectionManager
        {
            get => IntersectionTemplateManager.Instance;
            set => IntersectionTemplateManager.Instance = value;
        }

        public static ulong UserId { get; } = PlatformService.active ? PlatformService.user.userID.AsUInt64 : 0;
        protected static Dictionary<ulong, string> Authors { get; } = new Dictionary<ulong, string>();
        public static string GetAuthor(ulong steamId)
        {
            if (PlatformService.active)
            {
                try
                {
                    if (!Authors.TryGetValue(steamId, out string author))
                    {
                        author = new Friend(new UserID(steamId)).personaName;
                        Authors[steamId] = author;
                    }
                    return author;
                }
                catch (Exception error)
                {
                    Mod.Logger.Error("Could not get author name", error);
                }
            }
            return Localize.Template_UnknownAuthor;
        }

        public abstract SavedString Saved { get; }

        static TemplateManager()
        {
            StyleManager = new StyleTemplateManager();
            IntersectionManager = new IntersectionTemplateManager();
        }

        public abstract void AddTemplate(Template template);
        public abstract void Load();

        public static void Reload()
        {
            Mod.Logger.Debug($"{nameof(TemplateManager)}.{nameof(Clear)}");

            StyleManager.Load();
            IntersectionManager.Load();
        }
        public static void Clear()
        {
            Mod.Logger.Debug($"{nameof(TemplateManager)}.{nameof(Clear)}");

            StyleManager.Clear(true);
            IntersectionManager.Clear(true);
            Authors.Clear();
        }
    }
    public abstract class TemplateManager<TemplateType> : TemplateManager
        where TemplateType : Template<TemplateType>
    {
        public static TemplateManager<TemplateType> Instance { get; protected set; }

        protected abstract string DefaultName { get; }
        protected Dictionary<Guid, TemplateType> TemplatesDictionary { get; } = new Dictionary<Guid, TemplateType>();
        public IEnumerable<TemplateType> Templates => TemplatesDictionary.Values;

        #region SAVE&LOAD

        public void TemplateChanged(TemplateType template)
        {
            if (template.IsAsset)
                Loader.SaveTemplateAsset(template.Asset);
            else
                Save();
        }

        public override void Load()
        {
            try
            {
                Clear();
                var xml = Saved.value;
                if (!string.IsNullOrEmpty(xml))
                {
                    var config = Loader.Parse(xml);
                    FromXml(config);
                }

                Mod.Logger.Debug($"{typeof(TemplateType).Name} was loaded: {TemplatesDictionary.Count} items");
            }
            catch (Exception error)
            {
                Mod.Logger.Error($"Could not load {typeof(TemplateType).Name}", error);
            }
        }
        protected void Save()
        {
            try
            {
                var config = Loader.GetString(ToXml());
                Saved.value = config;

                Mod.Logger.Debug($"{typeof(TemplateType).Name} was saved: {TemplatesDictionary.Count} items");
            }
            catch (Exception error)
            {
                Mod.Logger.Error($"Could not save {typeof(TemplateType).Name}", error);
            }
        }

        public virtual void Clear(bool clearAssets = false)
        {
            if (clearAssets)
                TemplatesDictionary.Clear();
            else
            {
                var toDelete = Templates.Where(t => !t.IsAsset).Select(t => t.Id).ToArray();
                foreach (var id in toDelete)
                    TemplatesDictionary.Remove(id);
            }
        }
        public void DeleteAll()
        {
            Clear();
            Save();
        }

        public bool MakeAsset(TemplateType template)
        {
            if (template.IsAsset)
                return true;

            var asset = new TemplateAsset(template);
            var saved = Loader.SaveTemplateAsset(asset);
            if (saved)
                Save();

            return saved;
        }
        #endregion

        #region ADD&DELETE
        public override void AddTemplate(Template template)
        {
            if (template is TemplateType templateType)
                AddTemplate(templateType);
        }
        public void AddTemplate(TemplateType template)
        {
            if (NeedAdd(template))
                TemplatesDictionary[template.Id] = template;
        }
        private bool NeedAdd(TemplateType template)
        {
            if (TemplatesDictionary.TryGetValue(template.Id, out TemplateType existTemplate))
            {
                if (!template.IsAsset)
                    return false;

                if (existTemplate.IsAsset)
                {
                    if (!template.Asset.IsWorkshop)
                        return false;

                    if (existTemplate.Asset.IsWorkshop)
                    {
                        if (!template.Asset.IsLocalFolder)
                            return false;

                        if (existTemplate.Asset.IsLocalFolder)
                            return false;
                    }
                }
            }

            return true;
        }

        public void DeleteTemplate(TemplateType template)
        {
            TemplatesDictionary.Remove(template.Id);
            OnDeleteTemplate(template);

            Save();
        }
        protected virtual void OnDeleteTemplate(TemplateType template) { }

        #endregion

        #region NAME

        public string GetNewName(string newName = null)
        {
            if (string.IsNullOrEmpty(newName))
                newName = DefaultName;

            var i = 0;
            foreach (var template in Templates.Where(t => t.Name.StartsWith(newName)))
            {
                if (template.Name.Length == newName.Length && i == 0)
                    i = 1;
                else if (int.TryParse(template.Name.Substring(newName.Length), out int num) && num >= i)
                    i = num + 1;
            }
            return i == 0 ? newName : $"{newName} {i}";
        }
        public bool ContainsName(string name, TemplateType ignore) => TemplatesDictionary.Values.Any(t => t != ignore && t.Name == name);

        #endregion

        #region XML

        public virtual XElement ToXml()
        {
            var config = new XElement("C");

            foreach (var template in Templates)
            {
                if (!template.IsAsset)
                    config.Add(template.ToXml());
            }

            return config;
        }

        protected virtual void FromXml(XElement config)
        {
            foreach (var templateConfig in config.Elements(Template.XmlName))
            {
                if (Template.FromXml(templateConfig, out TemplateType template) && !TemplatesDictionary.ContainsKey(template.Id))
                    TemplatesDictionary[template.Id] = template;
            }
        }

        #endregion
    }

    public abstract class TemplateManager<TemplateType, Item> : TemplateManager<TemplateType>
        where TemplateType : Template<TemplateType>
    {
        public bool AddTemplate(Item item, out TemplateType template) => AddTemplate(GetNewName(), item, out template);
        protected bool AddTemplate(string name, Item item, out TemplateType template)
        {
            template = CreateInstance(name, item);
            AddTemplate(template);
            Save();
            return true;
        }
        protected abstract TemplateType CreateInstance(string name, Item item);
    }
    public class StyleTemplateManager : TemplateManager<StyleTemplate, Style>
    {
        public static new StyleTemplateManager Instance
        {
            get => TemplateManager<StyleTemplate>.Instance as StyleTemplateManager;
            set => TemplateManager<StyleTemplate>.Instance = value;
        }

        protected override string DefaultName => Localize.Template_NewTemplate;
        public override SavedString Saved => Settings.Templates;

        private Dictionary<Style.StyleType, Guid> DefaultTemplates { get; } = new Dictionary<Style.StyleType, Guid>();
        public bool IsDefault(StyleTemplate template) => DefaultTemplates.TryGetValue(template.Style.Type, out Guid id) && template.Id == id;
        public IEnumerable<StyleTemplate> GetTemplates(Style.StyleType group) => Templates.Where(t => (t.Style.Type & group & Style.StyleType.GroupMask) != 0);

        protected override StyleTemplate CreateInstance(string name, Style style) => new StyleTemplate(name, style);

        public override void Clear(bool clearAssets = false)
        {
            base.Clear(clearAssets);

            var pairs = DefaultTemplates.ToArray();
            foreach (var pair in pairs)
            {
                if (!TemplatesDictionary.ContainsKey(pair.Value))
                    DefaultTemplates.Remove(pair.Key);
            }
        }

        public bool DuplicateTemplate(StyleTemplate template, out StyleTemplate duplicate)
            => AddTemplate(GetNewName($"{template.Name} {Localize.Template_DuplicateTemplateSuffix}"), template.Style, out duplicate);

        protected override void OnDeleteTemplate(StyleTemplate template)
        {
            if (template.IsDefault)
                DefaultTemplates.Remove(template.Style.Type);
        }
        public void ToggleAsDefaultTemplate(StyleTemplate template)
        {
            if (template.IsDefault)
                DefaultTemplates.Remove(template.Style.Type);
            else
                DefaultTemplates[template.Style.Type] = template.Id;

            Save();
        }

        public T GetDefault<T>(Style.StyleType type) where T : Style
        {
            if (DefaultTemplates.TryGetValue(type, out Guid id) && TemplatesDictionary.TryGetValue(id, out StyleTemplate template) && template.Style.Copy() is T tStyle)
                return tStyle;
            else
                return Style.GetDefault<T>(type);
        }

        public override XElement ToXml()
        {
            var config = base.ToXml();

            foreach (var def in DefaultTemplates)
            {
                var defaultConfig = new XElement("D");
                defaultConfig.Add(new XAttribute("T", (int)def.Key));
                defaultConfig.Add(new XAttribute("Id", def.Value));

                config.Add(defaultConfig);
            }

            return config;
        }

        protected override void FromXml(XElement config)
        {
            base.FromXml(config);

            foreach (var defaultConfig in config.Elements("D"))
            {
                var styleType = (Style.StyleType)defaultConfig.GetAttrValue<int>("T");
                var templateId = defaultConfig.GetAttrValue<Guid>("Id");

                if (TemplatesDictionary.ContainsKey(templateId))
                    DefaultTemplates[styleType] = templateId;
            }
        }
    }
    public class IntersectionTemplateManager : TemplateManager<IntersectionTemplate, Markup>
    {
        public static new IntersectionTemplateManager Instance
        {
            get => TemplateManager<IntersectionTemplate>.Instance as IntersectionTemplateManager;
            set => TemplateManager<IntersectionTemplate>.Instance = value;
        }

        protected override string DefaultName => Localize.Preset_NewPreset;
        public override SavedString Saved => Settings.Intersections;

        protected override IntersectionTemplate CreateInstance(string name, Markup markup) => new IntersectionTemplate(name, markup);

        public bool AddTemplate(Markup markup, Image image, out IntersectionTemplate template)
        {
            if (AddTemplate(GetNewName(), markup, out template))
            {
                if (Loader.SaveScreenshot(image, template.Id))
                    template.Preview = image.CreateTexture();
                return true;
            }
            else
                return false;
        }
        protected override void FromXml(XElement config)
        {
            base.FromXml(config);

            foreach (var template in Templates.Where(t => !t.IsAsset))
            {
                if (Loader.LoadScreenshot(template.Id, out Image image))
                    template.Preview = image.CreateTexture();
            }
        }
    }
}
