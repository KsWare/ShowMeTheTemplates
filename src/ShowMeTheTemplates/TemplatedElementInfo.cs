using System;
using System.Collections.Generic;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace ShowMeTheTemplates {

	internal class TemplatedElementInfo {

		private readonly Type _elementType;

		public Type ElementType => _elementType;

		public IEnumerable<PropertyInfo> TemplateProperties { get; }
		public IEnumerable<PropertyInfo> StyleProperties { get; }

		public TemplatedElementInfo(Type elementType, IEnumerable<PropertyInfo> templatedProperties, IEnumerable<PropertyInfo> styleProperties) {
			_elementType = elementType;
			TemplateProperties = templatedProperties;
			StyleProperties = styleProperties;
		}

		public static IEnumerable<TemplatedElementInfo> GetTemplatedElements(Assembly assem) {
			var frameworkTemplateType = typeof(FrameworkTemplate);
			var styleType = typeof(Style);

			foreach (var type in assem.GetTypes()) {
				if (type.IsAbstract) continue;
				if (type.ContainsGenericParameters) continue;
				if (type.GetConstructor(new Type[] { }) == null) continue;

				var templatedProperties = new List<PropertyInfo>();
				var styleProperties = new List<PropertyInfo>();
				foreach (var prop in type.GetProperties(BindingFlags.Instance | BindingFlags.Public)) {
					if (frameworkTemplateType.IsAssignableFrom(prop.PropertyType))
						templatedProperties.Add(prop);
					if (styleType.IsAssignableFrom(prop.PropertyType))
						styleProperties.Add(prop);
					
				}

				if (templatedProperties.Count + styleProperties.Count== 0) continue;
				if (type == typeof(ContextMenu)) ;
				yield return new TemplatedElementInfo(type, templatedProperties, styleProperties);
			}
		}

	}

}