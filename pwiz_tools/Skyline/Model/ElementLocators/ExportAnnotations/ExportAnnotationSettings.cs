﻿using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.DocSettings;

namespace pwiz.Skyline.Model.ElementLocators.ExportAnnotations
{
    public class ExportAnnotationSettings : Immutable
    {
        public static ExportAnnotationSettings EMPTY = new ExportAnnotationSettings
        {
            ElementTypes = ImmutableList<string>.EMPTY,
            PropertyNames = ImmutableList<string>.EMPTY,
            AnnotationNames = ImmutableList<string>.EMPTY
        };

        public static ExportAnnotationSettings GetExportAnnotationSettings(IEnumerable<ElementHandler> selectedHandlers,
            IEnumerable<string> selectedAnnotationNames, IEnumerable<string> selectedProperties, bool removeBlankRows)
        {
            return EMPTY
            .ChangeElementTypes(selectedHandlers.Select(handler => handler.Name))
            .ChangeAnnotationNames(selectedAnnotationNames)
            .ChangePropertyNames(selectedProperties)
            .ChangeRemoveBlankRows(removeBlankRows);
        }

        public static ExportAnnotationSettings AllAnnotations(SrmDocument document)
        {
            return EMPTY.ChangeElementTypes(ElementHandler
                    .GetElementHandlers(SkylineDataSchema.MemoryDataSchema(document, DataSchemaLocalizer.INVARIANT))
                    .Select(handler => handler.Name))
                .ChangeAnnotationNames(
                    document.Settings.DataSettings.AnnotationDefs.Select(annotationDef => annotationDef.Name))
                .ChangePropertyNames(new[] {@"Note"});
        }

        public static string[] GetAllAnnotationNames(ImmutableList<AnnotationDef> annotationDefs, List<ElementHandler> selectedHandlers)
        {
            var annotationTargets = selectedHandlers.Aggregate(AnnotationDef.AnnotationTargetSet.EMPTY,
                (value, handler) => value.Union(handler.AnnotationTargets));
            return annotationDefs.Where(
                    annotationDef =>
                        annotationDef.AnnotationTargets.Intersect(annotationTargets).Any())
                .Select(annotationDef => annotationDef.Name).OrderBy(name => name).ToArray();
        }

        public ImmutableList<string> ElementTypes { get; private set; }
        public ImmutableList<string> PropertyNames { get; private set; }
        public ImmutableList<string> AnnotationNames { get; private set; }
        public bool RemoveBlankRows { get; private set; }

        public ExportAnnotationSettings ChangeElementTypes(IEnumerable<string> types)
        {
            return ChangeProp(ImClone(this), im => im.ElementTypes = NormalizeList(types));
        }

        public ExportAnnotationSettings ChangePropertyNames(IEnumerable<string> properties)
        {
            return ChangeProp(ImClone(this), im => im.PropertyNames = NormalizeList(properties));
        }

        public ExportAnnotationSettings ChangeAnnotationNames(IEnumerable<string> annotations)
        {
            return ChangeProp(ImClone(this), im => im.AnnotationNames = NormalizeList(annotations));
        }

        public ExportAnnotationSettings ChangeRemoveBlankRows(bool removeBlankRows)
        {
            return ChangeProp(ImClone(this), im => im.RemoveBlankRows = removeBlankRows);
        }

        private static ImmutableList<string> NormalizeList(IEnumerable<string> list)
        {
            if (list == null)
            {
                return ImmutableList.Empty<string>();
            }
            return ImmutableList.ValueOf(list.Distinct());
        }
    }
}
