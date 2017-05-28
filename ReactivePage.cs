/*
                GNU GENERAL PUBLIC LICENSE
                           Version 3, 29 June 2007
     Copyright (C) 2007 Free Software Foundation, Inc. <http://fsf.org/>
     Everyone is permitted to copy and distribute verbatim copies
     of this license document, but changing it is not allowed.

        Author: Michael J. Froelich
 */

using FAP;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using React;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

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

		private static React.TinyIoC.TinyIoCContainer container;
		private IReactComponent component;
		private static bool hasinit = false;

		//Object == default props, used if unspecified in a get function. String == default HTML, including hosted scripts and CSS
		//These are DEFAULTS, this means they are in some way static but aren't static for all reactive pages.
		//This unfortunately necessitates using a container, fortunately the container is only accessed if the props are changed
		private static Dictionary<string, Component> defaults = new Dictionary<string, Component>();

		public static string BabelVersion { get; set; } = "6.15.0";

		private Component cvars { 
			get {
				if (Path != null)
					return defaults[Path];
				else
					return null;
			}
			set {
				defaults[Path] = value;
			}
		}

		/// <summary>
		/// Set a function which will be called when accessing this page through a "get" HTTP method. Return using
		/// Encoding.BigEndianUnicode for binary files (no warranties, no guarantees).
		/// </summary>
		/// <value>The get function</value>
		public Func<string, string, object> get {get; set;}


		/// <summary>
		/// Shortest interval in which new javascript has to be loaded, 5 seconds is good.
		/// </summary>
		/// <value>Time</value>
		public static int ScriptReloadTime { get; set; } = 500;

		/// <summary>
		/// Freely accessible default props. Will be overidden if props are returned from a get function.
		/// </summary>
		public object Props {
			get {
				return cvars.Props;
			}
			set {
				cvars.Props = value;
			}
		}

		private string componentName;

		private string ComponentName {
			get {
				if (component == null) {
					componentName = cvars.Name;
				}
				return componentName;
			}
			set {
				componentName = value;
			}
		}

		/// <summary>
		/// Determines whether or not to generate HTML pre-amble, including script includes, 
		/// or to generate just the component's HTML. The default is false. <3 PHP nerds.
		/// </summary>
		public bool IsSPA {
			get {
				return cvars.IsSPA;
			}
			set {
				cvars.IsSPA = value;
			}
		}

		/// <summary>
		/// Set false to not include the jQuery library as a hosted script in the resultant HTML from this page. Set true otherwise. Default is true.
		/// </summary>
		public bool IncludeJQuery {
			get {
				return !String.IsNullOrEmpty(cvars.Scripts[3]);
			}

			set {
				var s = cvars.Scripts;
				if (!value) {
					s[3] = String.Empty;
				}
				if (value) {
					s[3] = Component.JQUERY;
				}
			}
		}

		/// <summary>
		/// Set false to not include the Babel Script library as a hosted script in the resultant HTML from this page. Also enable the script type as babel for client side transpiling.
		/// </summary>
		public bool IncludeBable {
			get {
				return !String.IsNullOrEmpty(cvars.Scripts[2]);
			}

			set {
				var s = cvars.Scripts;
				if (!value) {
					s[2] = String.Empty;
				}
				if (value) {
					s[2] = Component.BABEL;
				}
			}
		}

		/// <summary>
		/// Set false to not include the react libraries and babel as a hosted scripts when using the SPA. Set true otherwise. Default is true.
		/// </summary>
		public bool IncludeReact {
			get {
				return !String.IsNullOrEmpty(cvars.Scripts[0]);
			}

			set {
				var s = cvars.Scripts;
				if (!value) {
					s[0] = String.Empty;
					s[1] = String.Empty;
				}
				if (value) {
					s[0] = Component.REACT;
					s[1] = Component.REACTDOM;
				}
			}
		}
		static int ecmaStage = 1;

		/// <summary>
		/// Gets or sets the ECMA stage. This is passed into Babel as a preset of "stage-*"
		/// 0 uses ECMA script proposal features, there are more proposals.
		/// 3 uses ECMA script candidate features, there are fewer candidates.
		/// </summary>
		/// <value>Stage, 0 to 3</value>
		public static int EcmaStage {
			get {
				return ecmaStage;
			}
			set {
				if (value >= 0 && value <= 3) {
					if (hasinit) {
						ReactSiteConfiguration.Configuration.BabelConfig.Presets.Remove("stage-" + ecmaStage.ToString());
						ReactSiteConfiguration.Configuration.BabelConfig.Presets.Add("stage-" + value.ToString());
						ecmaStage = value;
					}
				}
			}
		}
		static int ecmaYear = 2015;

		public static ISet<string> BabelPresets => ReactSiteConfiguration.Configuration.BabelConfig.Presets;

		public static ISet<string> BabelPlugins => ReactSiteConfiguration.Configuration.BabelConfig.Plugins;

		/// <summary>
		/// Gets or sets the ECMA stage. This is passed into Babel as a preset of "stage-*"
		/// 0 uses ECMA script proposal features, there are more proposals.
		/// 3 uses ECMA script candidate features, there are fewer candidates.
		/// </summary>
		/// <value>Stage, 0 to 3</value>
		public static int EcmaYear {
			get {
				return ecmaYear;
			}
			set {
				if (value >= 2015 && value <= 2017) {
					if (hasinit) {
						ReactSiteConfiguration.Configuration.BabelConfig.Presets.Remove("es" + ecmaYear + "-no-commonjs");
						ReactSiteConfiguration.Configuration.BabelConfig.Presets.Add("es" + value + "-no-commonjs");
						ecmaYear = value;
					}
				}
			}
		}

		/// <summary>
		/// Initialize this instance.
		/// </summary>
		private static void Initialize()
		{
			if (hasinit)
				return;
			Initializer.Initialize(registration => registration.AsSingleton());
			container = React.AssemblyRegistration.Container;
			// Register some components that are normally provided by the integration library
			// (eg. React.AspNet or React.Web.Mvc6)
			container.Register<ICache, NullCache>();
			container.Register<IFileSystem, SimpleFileSystem>();
			//EcmaStage = 0;//Here at FAP we like to live dangerously
			//environment = ReactEnvironment.Current;
			//defaults = new Dictionary<string, Tuple<object, string>>();
			hasinit = true;
		}

		/// <summary>
		/// To include CSS userscripts.
		/// </summary>
		/// <param name="Pathname">Path or URL to the CSS file (ie yourwebsite.com/css/main.css)</param>
		/// <returns></returns>
		public void IncludeCSS(string Pathname)
		{
			cvars.Style += "<link rel=\"stylesheet\" href=\"" + Pathname + "\">";
		}

		/// <summary>
		/// Add HTML metadata typically found in the head of the HTML file for a single page application.
		/// For example: with meta = theme-color and content = #ff0000, the browser will appear red for mobile clients
		/// </summary>
		/// <param name="meta">The meta data name label (description, theme-color, viewport etc)</param>
		/// <param name="content">The content of that meta data</param>
		public void AddMeta(string meta, string content)
		{
			cvars.Metadata += "<meta name=\"" + meta + "\" content=\"" + content + "\">";
		}

		/// <summary>
		/// As the AddMeta, adds a charset (such as utf-8) to a single page application
		/// </summary>
		/// <param name="charset"></param>
		public void Charset(string charset)
		{
			cvars.Metadata += "<meta charset=\"" + charset + "\">";
		}

		/// <summary>
		/// Adds scripts for both the ReactJS engine as well as to be included within script tags on the resultant page.
		/// If given a valid URL to "Pathname" or if useRenderMachine is false, this method will create script tags with a source property of your URL.
		/// Otherwise, it will search the file system. You may need to include it again if IsSPA is true.
		/// Please remember: the order that you include scripts absolutely does matter.
		/// </summary>
		/// <param name="Pathname">The pathname, beginning from the binary, to the JS/JSX script file to be included.</param>
		/// <param name="useRenderMachine">True to pass this script to the ReactJS render machine, false for external libraries which will be linked as script sources for your SPA
		/// Basically, if useRenderMachine = false then this line: cvars.Scripts.Add("<script src='" + Pathname + "'></script>");</param>
		/// <returns></returns>
		public void IncludeScript(string Pathname, bool useRenderMachine = true)
		{
			if (!hasinit)
				Initialize();
			Script s = new Script();
			s.isJSX = Pathname.EndsWith(".jsx");
			if (IsURL(Pathname) || !useRenderMachine) {
				if (s.isJSX) {
					cvars.Scripts.Add("\n\t\t\t<script type=\"text/babel\" src='" + Pathname + "'></script>");
				} else {
					cvars.Scripts.Add("\n\t\t\t<script src='" + Pathname + "'></script>");
				}
			} else {
				try {
					if (useRenderMachine) {
						string currentDirectory = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);
						string scriptFullPath = System.IO.Path.Combine(currentDirectory, Pathname);
						if (s.isJSX) {
							ReactSiteConfiguration.Configuration.SetReuseJavaScriptEngines(false).AddScript(Pathname);
						} else {
							ReactSiteConfiguration.Configuration.SetReuseJavaScriptEngines(false).AddScriptWithoutTransform(Pathname);
						}
						if (!cvars.ComponentScriptPathinfo.ContainsKey(scriptFullPath)) {
							loadScriptFile(s, scriptFullPath);
							cvars.ComponentScriptPathinfo.Add(scriptFullPath, s);
						}
					}
				} catch (Exception e) {
					Console.Error.WriteLine("10: FAP.React script include error: \n" + e.Message);
				}
			}
		}

		public void IncludeScripts(IEnumerable<string> Pathnames, bool useRenderMachine = true)
		{
			foreach (string s in Pathnames)
				IncludeScript(s, useRenderMachine);
		}

		/// <summary>
		/// Used to run a script in Babel before running your component file, such as typings or mock javascript.
		/// This will not be included in the SPA output. These scripts will only ever run serverside.
		/// </summary>
		/// <param name="Pathname">Pathname.</param>
		public void IncludeMockScript(string Pathname)
		{
			if (Pathname.EndsWith(".jsx")) {
				ReactSiteConfiguration.Configuration.SetReuseJavaScriptEngines(false).AddScript(Pathname);
			} else {
				ReactSiteConfiguration.Configuration.SetReuseJavaScriptEngines(false).AddScriptWithoutTransform(Pathname);
			}
		}

		public void IncludeMockScripts(IEnumerable<string> Pathnames)
		{
			foreach (string s in Pathnames)
				IncludeMockScript(s);																																		
		}

		private Script loadScriptFile(Script s, string scriptFullPath)
		{
			s.ComponentScript = File.ReadAllText(scriptFullPath);
			if (s.isJSX)
				s.RenderedComponentScript = ReactEnvironment.Current.Babel.Transform(s.ComponentScript);
			s.Size = new FileInfo(scriptFullPath).Length;
			s.ScriptPath = scriptFullPath;
			return s;
		}

		static bool stillrunnning = false;

		async void scriptRegister()
		{
			try {
				while (defaults != null) {
					stillrunnning = false;
					foreach (Component v in defaults.Values) {
						if (v.ComponentScriptPathinfo != null && v.ComponentScriptPathinfo.Count > 0)
							foreach (Script s in v.ComponentScriptPathinfo.Values) {
								if (!String.IsNullOrEmpty(s.ScriptPath)) {
									FileInfo f = new FileInfo(s.ScriptPath);
									if (f.Exists && f.Length != s.Size) {
										loadScriptFile(s, s.ScriptPath);
									} else if (!f.Exists) {
										v.ComponentScriptPathinfo.Remove(s.ScriptPath);
									}
								}
							}
					}
					stillrunnning = true;
					await Task.Delay(ScriptReloadTime);
				}
			} catch (Exception e) {
				Console.Error.WriteLine("13: " + DateTime.UtcNow + " Script Load Error: \n\n" + e.StackTrace);
				await Task.Delay(ScriptReloadTime + 100);
				if (!stillrunnning)
					Task.Factory.StartNew(scriptRegister);
			}
		}

		/// <summary>
		/// This should be called by FAP when serving and not by the user
		/// </summary>
		public ReactivePage()
		{
		}

		/// <summary>
		/// A simplified constructor that assumes the path is the component name as all lower case
		/// </summary>
		/// <param name="PathAndComponentName">Path and component name.</param>
		public ReactivePage(string PathAndComponentName, object initialProps = null)
			: this(PathAndComponentName.ToLower(), PathAndComponentName, initialProps)
		{
		}

		/// <summary>
		/// This may be called at startup for each component that may be served via FAP
		/// </summary>
		/// <param name="path">The path to this page, ie 127.0.0.1/path?</param>
		/// <param name="componentName">The component name as found in JSX files</param>
		/// <param name="initialProps">The initial properties passed to the React renderer, it can otherwise be changed via the "Props" property or by assigning a "get" (lowercase) function and returning a JSON string from it</param>
		public ReactivePage(string path, string componentName, object initialProps)
			: base(path) //even here we must call the base constructor
		{   //For overridden classes: public public ReactivePage(...
			//                                          : base(string p, string c, object i)
			Initialize();
			ComponentName = componentName;
			defaults.Add(path, new Component {
				Get = get,
				Props = initialProps,
				Name = componentName
			});
			Task.Factory.StartNew(scriptRegister);
		}

		private object oldprops;
		private string oldtext;

		/// <summary>
		/// Get function, called by FAP internals, not to be called or overidden by users. This will call the get (lowercase) function and deserialise its output for the props object.
		/// </summary>
		/// <param name="queryString"></param>
		/// <param name="messageContent"></param>
		/// <returns></returns>
		public sealed override string Get(string queryString, string messageContent)
		{
			bool debug = false;
			#if DEBUG
			debug = true;
			#endif
			object props = null;
			try {
				if (base.get != null)
					props = JsonConvert.DeserializeObject<object>(base.get(queryString, messageContent));
				else if (this.get != null || cvars.Get != null) {
					if(get == null) get = cvars.Get;
					props = get(queryString, messageContent);
					if (props is string) {
						string propsstring = props as string;
						if (propsstring[0] == '{' && propsstring[propsstring.Length - 1] == '}' ||
						   propsstring[0] == '[' && propsstring[propsstring.Length - 1] == ']') {
							props = JsonConvert.DeserializeObject<object>(propsstring);
						}
					}
				}	
			} catch (Exception e) {
				Console.Error.WriteLine("11: Json.NET Parse error: " + e.Message);
			}
			//Do not run the rendering machine if the props are unchanged.
			if (oldprops == null || oldtext == null || debug ||
			             !JToken.DeepEquals(JToken.FromObject(props), JToken.FromObject(oldprops))) {
				if (props == null) {
					props = cvars.Props;
				}
				oldprops = props;
				try {
					var environment = React.AssemblyRegistration.Container.Resolve<IReactEnvironment>();
					component = environment.CreateComponent(ComponentName, props);

					string html;
					bool hasreact = !String.IsNullOrEmpty(cvars.Scripts[0]);
					if (hasreact)
						html = component.RenderHtml(false, true);
					else
						html = component.RenderHtml();
					if (IsSPA) {
						var output = new StringBuilder(Component.OPENINGHEADER + cvars.Metadata + cvars.Style + Component.CLOSINGHEADER + html);
						if(!cvars.Webscriptpack)
							for (int i = 0; i < cvars.Scripts.Count; i++) {
								if (!String.IsNullOrEmpty(cvars.Scripts[i]))
									output.Append(cvars.Scripts[i]);
							}
						bool hasBabel = !String.IsNullOrEmpty(cvars.Scripts[2]);
						foreach (Script s in cvars.ComponentScriptPathinfo.Values) {
							if (hasBabel && s.isJSX) {
								output.Append(Component.BabelType);
							} else {
								output.Append(Component.RegularType);
							}
							if (s.isJSX && !hasBabel) {
								if (String.IsNullOrEmpty(s.RenderedComponentScript))
									s.RenderedComponentScript = ReactEnvironment.Current.Babel.Transform(s.ComponentScript);
								output.Append(s.RenderedComponentScript);
							} else {
								output.Append(s.ComponentScript);
							}
							output.Append("\n\n</script>\n\n");
						} 
						if (hasreact || cvars.Webscriptpack) /* && hasBabel)
                            output.Append(Component.BableType + component.RenderJavaScript() + "\n\n</script>\n\n");
                        else*/
                            output.Append(Component.RegularType + component.RenderJavaScript() + "\n\n</script>\n\n");
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

		private static bool IsURL(string source)
		{
			Uri uriResult;
			return Uri.TryCreate(source, UriKind.Absolute, out uriResult) && ((uriResult.Scheme == Uri.UriSchemeHttp) || (uriResult.Scheme == Uri.UriSchemeHttps));
		}

		/// <summary>
		/// Gets or sets the js folder for scripts received from the web.
		/// Default it: "../../js"
		/// 	This is the project folder.
		/// </summary>
		/// <value>The js folder</value>
		public static string JsFolder { get; set; } = "../../js";

		/// <summary>
		/// Loads all enabled hosted script sources, including the default scripts and the scripts added by IncludeScript(*, false)
		/// from the web and then into the ReactJS.NET engine. Usage of this function disables all hosted script sourcing.
		/// Hint: scripts added after running this function will still appear as hosted script sources
		/// </summary>
		public bool GetScriptsFromWeb()
		{
			try {
				var outputWeb = new StringBuilder();	 //Scripts that require sourcing and execution
				var outputDefaults = new StringBuilder();//Scripts that require sourcing but not execution
				System.Net.WebClient client = new System.Net.WebClient();
				for (int i = 0; i < cvars.Scripts.Count; i++) {
					string urls = cvars.Scripts[i];
					if (cvars.Scripts[i] != String.Empty) {
						int start = urls.IndexOf((char)39) + 1;
						int end = urls.LastIndexOf((char)39);
						string url = urls.Substring(start, end - start);
						if (i == 3 && EcmaStage != 0) { //There is no point loading the real jQuery at this point
							outputDefaults.Append(client.DownloadString(url)).Append("\n");
							url = Component.MOCKJQUERY;
							string webget = client.DownloadString(url);
							var jquerymockpath = System.IO.Path.Combine(JsFolder, "jquerymock.js");
							File.WriteAllText(jquerymockpath, webget.ToString());
							ReactSiteConfiguration.Configuration.SetReuseJavaScriptEngines(false).AddScript(jquerymockpath);
						} else if (IsURL(url)) {
							string webget = client.DownloadString(url);
							var outputCurrent = (i < 3) ? outputDefaults : outputWeb;
							if (url.EndsWith(".jsx")){
								outputCurrent.Append(ReactEnvironment.Current.Babel.Transform(webget));
							}
							else
								outputCurrent.Append(webget);
							outputCurrent.Append("\n");
						}
						cvars.Scripts[i] = String.Empty;
					}
				}
				if (outputWeb.Length > 0) {
					var p = System.IO.Path.Combine(JsFolder, "web.js");
					File.WriteAllText(p, removeComments(outputWeb.ToString()));
					IncludeScript(p);
				}
				if (outputDefaults.Length > 0) {
					var p = System.IO.Path.Combine(JsFolder, "react.js");
					File.WriteAllText(p, removeComments(outputDefaults.ToString()));
					cvars.ComponentScriptPathinfo.Add(p.ToString(),
						new Script{
							ComponentScript = outputDefaults.ToString(),
							isJSX = false,
							ScriptPath = p.ToString(),
							Size = new FileInfo(p.ToString()).Length
					});
				}
				cvars.Webscriptpack = true;
				return true;
			} catch (Exception e) {
				Console.Error.WriteLine("14: Getting scripts from Web has failed " + e.Message);
				return false;
			}
		}
		/// <summary>
		/// http://stackoverflow.com/questions/3524317/regex-to-strip-line-comments-from-c-sharp/3524689#3524689
		/// Thank you Timwi
		/// </summary>
		/// <returns>The comments.</returns>
		private string removeComments(string input)
		{
			const string blockComments = @"/\*(.*?)\*/";
			const string lineComments = @"//(.*?)\r?\n";
			const string strings = @"""((\\[^\n]|[^""\n])*)""";
			const string verbatimStrings = @"@(""[^""]*"")+";
			return System.Text.RegularExpressions.Regex.Replace(input, 
				blockComments + "|" + lineComments + "|" + strings + "|" + verbatimStrings,
               me => {
					if (me.Value.StartsWith("/*") || me.Value.StartsWith("//"))
						return me.Value.StartsWith("//") ? Environment.NewLine : "";
					return me.Value;// Keep the literal strings
				}, System.Text.RegularExpressions.RegexOptions.Singleline);
		}

		/// <summary>
		/// Private class used as a sort of smart structure
		/// </summary>
		private class Component
		{
			public const string REACT = "\n\t\t\t<script src='https://unpkg.com/react@latest/dist/react.min.js'></script>";
			public const string REACTDOM = "\n\t\t\t<script src='https://unpkg.com/react-dom@latest/dist/react-dom.min.js'></script>";
			public static readonly string BABEL = "\n\t\t\t<script src='https://unpkg.com/babel-standalone@" + BabelVersion + "/babel.min.js'></script>";
			public const string JQUERY = "\n\t\t\t<script src='http://ajax.googleapis.com/ajax/libs/jquery/1/jquery.min.js'></script>";
			public const string BabelType = "\n\t\t<script type=\"text/babel\">\n\n";
			public const string RegularType = "\n\t\t<script>";
			public const string OPENINGHEADER = "<!DOCTYPE html>\n\t<html>\n\t\t<head>\n\t\t\t\n";
			public const string CLOSINGHEADER = "\n\t\t\t\n\t\t</head>\n\t\t<body>\n";
			public const string FOOTER = "\n\t\t</body>\n\t</html>";
			internal const string MOCKJQUERY = "https://raw.githubusercontent.com/MichaelFroelich/jquery-mockjax/master/src/jquery.mockjax.js";

			public bool Webscriptpack = false;

			public bool IsSPA = true;

			public string Name;

			public object Props;

			public string Style = String.Empty;

			public string Metadata = String.Empty;

			public Func<string, string, object> Get;

			public List<string> Scripts = new List<string> {
				REACT,
				REACTDOM,
				BABEL,
				JQUERY
			};

			public Dictionary<string,Script> ComponentScriptPathinfo = new Dictionary<string,Script>();

		}

		private class Script
		{
			public bool isJSX;
			public string ScriptPath;
			public string ComponentScript;
			public string RenderedComponentScript;
			public long Size;
		}
	}
}
