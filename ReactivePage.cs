using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using React;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace FAP
{
	public class ReactivePage : Page
	{
		/*
        private static void Main(string[] args)
        {
            ///main.jsx
            //
            //var HelloWorld = React.createClass({
            //    render() {
            //        return (
            //            <a id="hello">
            //                Hello, I am the {this.props.name}
            //            </a>
            //        );
            //    }
            //});
            ///
            Server server = new Server();
            //Initialise a reactivePage with default props, you may also extend this class but may not override the Get function
            var reactivePage = new ReactivePage("hello", "HelloWorld", new { name = "the Computer" });
            //Define within the lowercase get function whatever logic is used to generate the props
            reactivePage.get = (a, b) => JsonConvert.SerializeObject(new { name = "not the Computer, but in fact " + a });
            //Direct FAP.React to the script file that defines the component
            reactivePage.IncludeScript("main.jsx");
            //Includes Angular JS as a hosted library
            reactivePage.IncludeScript("https://ajax.googleapis.com/ajax/libs/angularjs/1.4.9/angular.min.js");
            //Whether or not to include Jquery as a library
            reactivePage.IncludeJQuery = true;
            //Whether or not to include React as a library, including react.js and react-dom.js and browser.js for babel
            reactivePage.IncludeReact = true;
            //Whether or not this page is a SPA, as in, whether or not to generate HTML preamble or just the html component (good for appending)
            reactivePage.IsSPA = true;
            //Finally, add the page to the server
            server.AddPage(reactivePage);
            Thread.Sleep(-1); //Never forget to sleep.
        }
        */
		static React.TinyIoC.TinyIoCContainer container;
		IReactComponent component;
		static bool hasinit = false;

		//Object == default props, used if unspecified in a get function. String == default HTML, including hosted scripts and CSS
		//These are DEFAULTS, this means they are by nature static.
		static Dictionary<string, Component> defaults = new Dictionary<string, Component>();

		string componentName;

		string ComponentName {
			get {
				if (component == null) {
					componentName = defaults[Path].Name;
				}
				return componentName;
			}
			set {
				componentName = value;
			}
		}

		/// <summary>
		/// Determins whether or not to generate HTML pre-amble, including script includes, or to generate just the component's HTML
		/// </summary>
		public bool IsSPA {
			get {
				return defaults[Path].IsSPA;
			}
			set {
				defaults[Path].IsSPA = value;
			}
		}

		bool includeJQuery = true;

		/// <summary>
		/// Set true to include the jQuery library as a hosted script in the resultant HTML from this page. Set false otherwise. Default is true.
		/// </summary>
		public bool IncludeJQuery {
			get {
				return includeJQuery;
			}

			set {
				var s = defaults[Path].Scripts; //pronounced vares
				if (!value) {
					s[3] = String.Empty;
				}
				if (value) {
					s[3] = "http://ajax.googleapis.com/ajax/libs/jquery/1/jquery.min.js";
				}
				includeJQuery = value;
			}
		}

		bool includeReact;

		/// <summary>
		/// Set true to include the react libraries and babel as a hosted scripts in the resultant HTML from this page. Set false otherwise. Default is true.
		/// </summary>
		public bool IncludeReact {
			get {
				return includeReact;
			}

			set {
				var s = defaults[Path].Scripts;
				if (!value) {
					s[0] = String.Empty;
					s[1] = String.Empty;
					s[2] = String.Empty;
				}
				if (value) {
					s[0] = "https://fb.me/react-0.14.0.min.js";
					s[1] = "https://fb.me/react-dom-0.14.0.min.js";
					s[2] = "http://cdnjs.cloudflare.com/ajax/libs/babel-core/5.8.23/browser.min.js";
				}
				includeReact = value;
			}
		}

		static void Initialize()
		{
			if (hasinit)
				return;
			Initializer.Initialize(registration => registration.AsSingleton());
			container = React.AssemblyRegistration.Container;
			// Register some components that are normally provided by the integration library
			// (eg. React.AspNet or React.Web.Mvc6)
			container.Register<ICache, NullCache>();
			container.Register<IFileSystem, SimpleFileSystem>();
			//environment = ReactEnvironment.Current;
			//defaults = new Dictionary<string, Tuple<object, string>>();
			hasinit = true;
		}

		/// <summary>
		/// To include CSS userscripts.
		/// </summary>
		/// <param name="Pathname">Path to a .css file within the file directory</param>
		/// <returns></returns>
		public bool IncludeCSS(string Pathname)
		{
			if (Pathname.EndsWith(".css")) {
				try {
					using (StreamReader streamReader = new StreamReader(Pathname)) {
						defaults[Path].Style += "\n\n" + streamReader.ReadToEnd();
					}
					return true;
				} catch (Exception e) {
					Console.Error.WriteLine("13: CSS include failure, " + e.Message);
					return false;
				}
			} else {
				Console.Error.WriteLine("13: CSS include failure, seemingly not a css file. Check the file and try renaming it.");
				return false;
			}
		}

		/// <summary>
		/// Adds scripts for both the ReactJS engine as well as to be included within script tags on the resultant page.
		/// If given a valid URL to "Pathname", this function will create script tags with a source property of your URL.
		/// Otherwise, it will search the file system and include your Javascript file freely within script tags
		/// Please remember: the order that you include scripts absolutely does matter.
		/// </summary>
		/// <param name="Pathname">The pathname, beginning from the binary, to the JS/JSX script file to be included.</param>
		/// <param name="AvoidJSX">False if you wish to apply JSX transformations, true if you wish to avoid transformations.</param>
		/// <param name="useRenderMachine">True to pass this script to the ReactJS render machine, false if you simply wish for it to appear within script tags in the output</param>
		/// <returns></returns>
		public bool IncludeScript(string Pathname, bool AvoidJSX = false, bool useRenderMachine = true)
		{
			Initialize();
			Component cvars = defaults[Path];
			if (IsURL(Pathname)) {
				cvars.Scripts.Add(Pathname);
			} else {
				try {
					if (useRenderMachine) {
						if (!AvoidJSX) {
							ReactSiteConfiguration.Configuration.SetReuseJavaScriptEngines(false).AddScript(Pathname);
						} else
							ReactSiteConfiguration.Configuration.SetReuseJavaScriptEngines(false).AddScriptWithoutTransform(Pathname);
					}
					using (StreamReader streamReader = new StreamReader(Pathname)) {
						cvars.Userscripts.Add(new Tuple<bool, string>(AvoidJSX, streamReader.ReadToEnd()));
					}
				} catch (Exception e) {
					Console.Error.WriteLine("10: FAP.React script include error: " + e.Message);
					return false;
				}
			}
			return true;
		}

		/// <summary>
		/// This should be called by FAP when serving and not by the user
		/// </summary>
		public ReactivePage()
		{
		}

		/// <summary>
		/// This may be called at startup for each component that may be served via FAP
		/// </summary>
		/// <param name="path">The path to this page, ie 127.0.0.1/path?</param>
		/// <param name="componentName">The component name as found in JSX files, in camelcase if you must</param>
		/// <param name="initialProps">The initial properties passed to the React renderer, it can otherwise be changed via the "Props" property or by assigning a "get" (lowercase) function and returning a JSON string from it</param>
		public ReactivePage(string path, string componentName, object initialProps)
			: base(path) //even here we must call the base constructor
		{
			Initialize();
			ComponentName = componentName;
			defaults.Add(path, new Component { Props = initialProps, Name = componentName });
		}

		object oldprops;
		string oldtext;

		/// <summary>
		/// Get function, called by FAP internals, not to be called by users. This will call the get (lowercase) function and deserialise its output for the props object.
		/// </summary>
		/// <param name="queryString"></param>
		/// <param name="messageContent"></param>
		/// <returns></returns>
		public sealed override string Get(string queryString, string messageContent)
		{
			var cvars = defaults[Path];
			object props = cvars.Props;
			if (get != null) {
				try {
					props = JsonConvert.DeserializeObject<object>(get(queryString, messageContent));
				}   //Unfortunately, FAP requires for the API to remain Func<string, string, string> and will not recognise the function as <string, string, object>, which would be neat
                catch (Exception e) {
					Console.Error.WriteLine("11: Json.NET Parse error: " + e.Message);
				}
			}
			//Do not run the rendering machine if the props are unchanged.
			if (oldprops == null || !JToken.DeepEquals(JToken.FromObject(props), JToken.FromObject(oldprops))) {
				oldprops = props;
				try {
					var environment = React.AssemblyRegistration.Container.Resolve<IReactEnvironment>();
					component = environment.CreateComponent(ComponentName, props);
					var html = component.RenderHtml(renderServerOnly: true);
					if (IsSPA) {
						var output = new StringBuilder(Component.OPENINGHEADER + cvars.Style + Component.CLOSINGHEADER + html);
						foreach (string s in cvars.Scripts) {
							if (!String.IsNullOrEmpty(s))
								output.Append("\n\t\t\t<script src='" + s + "'></script>");
						}
						foreach (Tuple<bool, string> t in cvars.Userscripts) {
							if (t.Item1) { //It's not pretty, but it works
								output.Append("\n\t\t\t<script>\n" + t.Item2 + "\n\t\t\t</script>");
							} else {
								output.Append("\n\t\t\t<script type=\"text/babel\">\n" + t.Item2 + "\n\t\t\t</script>");
							}
						}
						output.Append(Component.FOOTER);
						oldtext = output.ToString();
					} else
						oldtext = html;
				} catch (Exception e) {
					Console.Error.WriteLine("12: ReactJS.NET render error: " + e.Message +
					(e.Message.Contains("Unable to resolve type:") ? "\n\tYou may need to update/upgrade your system" : ""));
				}
			}
			return oldtext;
		}

		static bool IsURL(string source)
		{
			Uri uriResult;
			return Uri.TryCreate(source, UriKind.Absolute, out uriResult) && ((uriResult.Scheme == Uri.UriSchemeHttp) || (uriResult.Scheme == Uri.UriSchemeHttps));
		}

		/// <summary>
		/// Private class used as a sort of smart structure
		/// </summary>
		private class Component
		{
			public bool IsSPA = true;

			public string Name;

			public const string OPENINGHEADER = "<!DOCTYPE html>\n\t<html>\n\t\t<head>\n\t\t\t<style>\n";

			public const string CLOSINGHEADER = "\n\t\t\t</style>\n\t\t</head>\n\t\t<body>\n";

			public const string FOOTER = "\n\t\t</body>\n\t</html>";

			public object Props;

			public string Style = String.Empty;

			public List<string> Scripts = new List<string> {
				"http://fb.me/react-0.14.0.min.js",
				"http://fb.me/react-dom-0.14.0.min.js",
				"http://cdnjs.cloudflare.com/ajax/libs/babel-core/5.8.23/browser.min.js",
				"http://ajax.googleapis.com/ajax/libs/jquery/1/jquery.min.js"
			};

			public List<Tuple<bool, string>> Userscripts = new List<Tuple<bool, string>>();

		}
	}
}
