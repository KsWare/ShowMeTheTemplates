using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Reflection;
using System.Windows;

namespace ShowMeTheTemplates {

	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : Application {

		public static IEnumerable<Type> GetTemplatePartTypes(Assembly assem) {
			foreach( var type in assem.GetTypes() ) {
				if( !type.IsPublic ) { continue; }
				if( type.GetCustomAttributes(typeof(TemplatePartAttribute), false).Length == 0 ) { continue; }
				yield return type;
			}
		}

		protected override void OnStartup(StartupEventArgs e) {
			base.OnStartup(e);

			var templatePartTypes = new List<Type>(GetTemplatePartTypes(Assembly.LoadWithPartialName("PresentationFramework")));
			templatePartTypes.Sort(delegate(Type lhs, Type rhs) { return lhs.Name.CompareTo(rhs.Name); });

			foreach( var type in templatePartTypes) {
				//Debug.WriteLine(string.Format("{0}: ", type.Name));
				foreach( TemplatePartAttribute attrib in type.GetCustomAttributes(typeof(TemplatePartAttribute), false) ) {
					Debug.WriteLine($"{type.Name}, {attrib.Name}, {attrib.Type.Name}");
				}
			}
		}

	}

}

