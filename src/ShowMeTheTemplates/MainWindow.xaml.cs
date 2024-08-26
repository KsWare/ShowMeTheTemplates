using System;
using System.Collections;
using System.Reflection;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.IO;
using System.Linq;
using System.Windows.Controls.Primitives;
using System.Windows.Forms.Integration;
using System.Xml;
using System.Windows.Markup;
using System.Windows.Navigation;
using System.Xml.Linq;
using KsWare.Presentation.XamlProcessing;

namespace ShowMeTheTemplates {


	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window {

		private Assembly _presentationFrameworkAssembly = Assembly.LoadWithPartialName("PresentationFramework");
		private Dictionary<Type, object> _typeElementMap = new Dictionary<Type, object>();
		private List<string> _filesToDeleteOnExit = new List<string>();

		public MainWindow() {
			InitializeComponent();
			bookLink.Click += bookLink_Click;
			Closed += Window1_Closed;
			themes.SelectionChanged += themes_SelectionChanged;
			DataContext =
				new List<TemplatedElementInfo>(
					TemplatedElementInfo.GetTemplatedElements(_presentationFrameworkAssembly));
			themes.SelectedIndex = 0;
		}

		private void bookLink_Click(object sender, RoutedEventArgs e) {
			System.Diagnostics.Process.Start("http://sellsbrothers.com/writing/wpfbook/");
		}

		private void themes_SelectionChanged(object sender, SelectionChangedEventArgs e) {
			var cb = (ComboBox) sender;
			var themeUri = new Uri((string) ((ComboBoxItem) cb.SelectedItem).Tag, UriKind.Relative);
			var themeResources = (ResourceDictionary) Application.LoadComponent(themeUri);
			ItemsPanel.Resources = themeResources;
		}

		private void Window1_Closed(object sender, EventArgs e) {
			foreach (var file in _filesToDeleteOnExit) {
				File.Delete(file);
			}
		}

		private void ElementHolder_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e) {
			var elementHolder = (ContentControl) sender;
			var elementInfo = (TemplatedElementInfo) elementHolder.DataContext;

			// Get element (cached)
			var element = GetElement(elementInfo.ElementType);

			// Create and show the element (some have to be shown to give up their templates...)
			ShowElement(elementHolder, element);

			// Fill the element (don't seem to need to do this, but makes it easier to see on the screen...)
			FillElement(element);
		}

		// Get the element from a cache based on the type
		// Used to avoid recreating a type twice and used so that when the WebBrowser needs to get the templates for each property, it knows where to look
		private object GetElement(Type elementType) {
			if (!_typeElementMap.ContainsKey(elementType)) {
				_typeElementMap[elementType] = _presentationFrameworkAssembly.CreateInstance(elementType.FullName);
			}

			return _typeElementMap[elementType];
		}

		// When the WF host has loaded (for each property on the currently selected control),
		// tell the WebBrowser to navigate to the property's template
		private void WindowsFormsHost_Loaded(object sender, RoutedEventArgs e) {
			var host = (WindowsFormsHost) sender;
			var prop = (PropertyInfo) host.DataContext;
			var browser = (System.Windows.Forms.WebBrowser) host.Child;
			var elementType = prop.ReflectedType;
			var element = GetElement(elementType);
			if (elementType == typeof(ContextMenu)) ;
			if (prop.Name == "Style") ;
			var v = prop.GetValue(element, null);
			if (v == null && prop.Name == "Style") {
				var resource = DependencyPropertyHelper.GetValueSource((DependencyObject) element, FrameworkElement.StyleProperty).BaseValueSource;
				var defaultStyleKey = (element as FrameworkElement)?.GetValue(DefaultStyleKeyProperty);
				if (defaultStyleKey != null) v = ItemsPanel.TryFindResource(defaultStyleKey);
			}
			ShowXaml(browser, v);
		}

		private void ShowXaml(System.Windows.Forms.WebBrowser browser, object obj) {
			if (obj == null) {
				browser.DocumentText = "(empty)";
				return;
			}

			// Write the template to a file so that the browser knows to show it as XML
			var filename = Path.GetTempFileName();
			File.Delete(filename);
			filename = Path.ChangeExtension(filename, "xml");

			// pretty print the XAML (for View Source)
			using (var writer = new XmlTextWriter(filename, System.Text.Encoding.UTF8)) {
				writer.Formatting = Formatting.Indented;
				XamlWriter.Save(obj, writer);
			}
			var xDocument = XDocument.Load(filename);
			//ConvertElementsToAttributes(xDocument.Root);
			XamlUtils.SimplifyValues(xDocument.Root);
			XamlUtils.SimplifyElementToAttribute(xDocument.Root);
			xDocument.Save(filename);
			// Show the template
			browser.Navigate(new Uri(@"file:///" + filename));
		}

		private static void ConvertElementsToAttributes(XElement element) {
			foreach (var childElement in element.Elements().ToList()) {
				var splitName = childElement.Name.LocalName.Split('.');
				if (splitName.Length!=2 || splitName[0]!=element.Name.LocalName) {
					ConvertElementsToAttributes(childElement);
					continue;
				}
				var valueElement = childElement.Elements().FirstOrDefault();
					if (valueElement == null) continue;

				string valueString;

				// Prüfen, ob es sich um eine MarkupExtension handelt
				if (IsMarkupExtension(valueElement)) {
					valueString = BuildMarkupExtensionString(valueElement);
				}
				else if (IsDirectlyConvertible(valueElement)) {
					valueString = ConvertToAttributeValue(valueElement);
				}
				else {
					// Falls das Element nicht direkt umwandelbar ist, rekursiv weitergehen
					ConvertElementsToAttributes(valueElement);
					continue;
				}

				// Setze das Attribut im übergeordneten Element
				element.SetAttributeValue(splitName[1], valueString);

				// Entferne das ursprüngliche Element
				childElement.Remove();
				
			}
		}

		private static string ConvertToAttributeValue(XElement element) {
			return element.Value.Trim();
		}

		private static bool IsMarkupExtension(XElement element) {
			// Prüft, ob es eine Markup-Erweiterung ist (erkennt bekannte Markup-Erweiterungen)
			return element.Name.LocalName.EndsWith("Resource") ||
			       element.Name.LocalName == "Binding" ||
			       element.Name.LocalName == "TemplateBinding" ||
			       element.Name.LocalName == "Static";
		}

		private static bool IsDirectlyConvertible(XElement element) {
			// Prüfen des Namens und des zugehörigen Namespace
			var localName = element.Name.LocalName;
			var namespaceName = element.Name.NamespaceName;

			// XAML-Präsentations-Namespace
			if (namespaceName == "http://schemas.microsoft.com/winfx/2006/xaml/presentation") {
				var simpleTypes = new HashSet<string> {
					"SolidColorBrush",
					"Color",
					"Thickness",
					"CornerRadius",
					"FontFamily",
					"FontWeight",
					"FontStyle",
					"FontStretch",
					"Brush",
					"Uri"
				};

				return simpleTypes.Contains(localName);
			}

			// System-Namespace für Typen wie <s:Boolean>
			if (namespaceName.StartsWith("clr-namespace:System")) {
				var systemTypes = new HashSet<string> {
					"Boolean",
					"Double",
					"Int32",
					"String"
				};

				return systemTypes.Contains(localName);
			}

			return false;
		}


		static string BuildMarkupExtensionString(XElement element) {
        var markupTypeName = element.Name.LocalName;
        var attributes = element.Attributes().ToArray();

        string result;
        if (attributes.Length == 1) {
            result = $"{{{markupTypeName} {attributes[0].Value}}}";
        } else {
            var attributeStrings = attributes.Select(attr => $"{attr.Name}={attr.Value}");
            result = $"{{{markupTypeName} {string.Join(", ", attributeStrings)}}}";
        }

        return result;
    }



		private void WebBrowser_Navigated(object sender, System.Windows.Forms.WebBrowserNavigatedEventArgs e) {
			// Queue the files to be deleted at shutdown (otherwise, View Source doesn't work)
			if (e.Url.IsFile) {
				_filesToDeleteOnExit.Add(e.Url.LocalPath);
			}
		}

		private void ShowElement(ContentControl elementHolder, object element) {
			elementHolder.Content = null;

			var elementType = element.GetType();
			if ((elementType == typeof(ToolTip)) ||
			    (elementType == typeof(Window))) {
				// can't be set as a child, but don't need to be shown, so do nothing
			}
			else if (elementType == typeof(NavigationWindow)) {
				var wnd = (NavigationWindow) element;
				wnd.WindowState = WindowState.Minimized;
				wnd.ShowInTaskbar = false;
				wnd.Show(); // needs to be shown once to "hydrate" the template
				wnd.Hide();
			}
			else if (typeof(ContextMenu).IsAssignableFrom(elementType)) {
				elementHolder.Content = new Label {
					Content = "ContextMenu",
					ContextMenu = (ContextMenu) element
				};
			}
			else if (typeof(Page).IsAssignableFrom(elementType)) {
				var frame = new Frame();
				frame.Content = element;
				elementHolder.Content = frame;
			}
			else {
				elementHolder.Content = element;
			}
		}

		private void FillElement(object element) {
			switch (element) {
				case MenuBase cm:
					cm.Items.Add(new MenuItem {Header = "Item A"});
					cm.Items.Add(new MenuItem {Header = "Item B"});
					break;
				case ContentControl cc: {
					cc.Content = "(some content)";

					if (cc is HeaderedContentControl hcc) {
						hcc.Header = "(a header)";
					}
					break;
				}
				case ItemsControl ic:
					ic.Items.Add("(an item)");
					break;
				case PasswordBox pw:
					pw.Password = "(a password)";
					break;
				case TextBoxBase tbb:
					tbb.AppendText("(some text)");
					break;
				case Page page:
					page.Content = "(some content)";
					break;
			}
		}

	}

}