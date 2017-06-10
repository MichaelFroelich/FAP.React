# FAP.React
Extension of the FAP.Page class (ReactivePage) for server side rendering with ReactJS.NET. This is an alternative network solution for ReactJS users, just assign a get function to return a JSON string

As of v.5 the basic boilerplate is as follows:
```
    /// <summary>
    /// This is the current basic cookie cutter of a FAP.ReactivePage
    /// Name this class the same as the Component name found in whatever script
    /// Using "nameof" allows you to rename everything C# side fairly quickly, right click and rename
    /// </summary>
    public class Index : ReactivePage
    {
        public Index() : base(nameof(Index), null)  //This constructor will actually be called by FAP.Server
        {                                               //Which isn't great, but all these functions are idempotent
            Engine.MinimiseBabelOutput = false;//This is good for debugging from a browser
            IncludeScript("main.jsx");      //Since this library is meant for cross platform and so more than one IDE
            IncludeCSS("css/main.css");     //Input files cannot be defined within the project or project files
            IncludeCSS("css/xbbcode.css");  //It must be defined in the code
            //IsSPA = true;                 //Due to the maturity of the IsSPA functionality, it's now set true as default
            get = GetDatabase;              //For proficient, safe development, this should be set with a state machine
          //(this as Page).get = (a,b) => JsonConvert.SerializeObject(GetDatabase(a,b)); //This also works, but why both?
        }
        
        public object GetDatabase(string a, string b)
        {
            return (new { name = "not the Computer, but in fact " + a });
        }
    }
```

And instantiated in regular FAP style by:
```
	public static void Main(string[] args)
	{
	    ReactivePage.DownloadScripts(); //This line is necessary if you have not downloaded scripts
	    Index page = new Index();
	    Server server = new Server(new[] { page });
	    for(;;)
	    	Thread.Sleep(-1);
	}
```
Where you may define whatever you like for the Get HTTP method. 

Remember to install [ReactJS](http://reactjs.net/guides/mono.html) if you're using Linux/mono. Alternative, you can use [my build](http://www.michaelfroelich.com/VroomJsNative.so). You'll also need v8, 3.14 is available in the Ubuntu repositories and a Windows version is available [here](https://github.com/MichaelFroelich/vroomjs-core/tree/master/native/compiled).

# How to use from the front end?

As previous of v.3, certain design recommendations have changed.

### jQuery append
Using jQuery or by using the function `document.getElementById("wherever").innerHTML()` you can certainly inject HTML snippets from FAP.React. This is the simplest and easiest solution, unfortunately this has no SEO value.
### IFrames
IFrames actually work and since sometime between 2012 and 2014 are even crawlable by Google's bot. Unfortunately there are still questions about IFrames, namely how using script and CSS from within the IFrame would work. On the other hand, if you are using FAP.React to return pure HTML I doubt there's any real issues. IFraming a SPA would solve this issue, but regardless you will still need to change your front facing server's settings.
### php
If you already have PHP installed, this might be the best option as it provides SEO (Search Engine Optimisation) and is relatively simple to implement. If PHP is already installed, you do not even need to change your front facing server's settings. From C#, set the IsSPA property from your ReactivePage object to false (meaning only the rendered HTML will be sent) and inject the HTML using something similar to this:
	
```
<?php 
	$d = file_get_contents('127.0.0.1:1024/hello');
	echo $d; 
>
```

Which may be placed where you wish your HTML blobs to appear. This will be executed before returning to the client, so you won't even need to configure a new location, as opposed to all other solutions.
	
### SPA
Ever so slightly more efficient than using PHP, as the same or fewer systems are involved, but requiring just a little more effort is the Single Page Application, but as of writing is the default for FAP.React. You will also need to change your front-facing server's settings, which for NGINX means adding a new location, then FAP.React will return a complete HTML page including script includes and CSS includes specified by location. As only cached HTML is returned from FAP, SEO should work identically to injecting the HTML with PHP without the added benefit of PHP where you might not actually need it.
		
# How to implement the SPA?
	
No matter what solution you find to this problem, SPA or IFrame/PHP/other, if you wish to use ReactJS style onClick methods such as:
```
return (
      <div onClick={this.handleClick}>
        You {text} this. Click to toggle.
      </div>
    );
```
Which you can find in full at: https://facebook.github.io/react/docs/interactivity-and-dynamic-uis.html

You will need to switch the IncludeReact property as true, and then include a link to your main.jsx if you wish to use reactjs dynamic functions. How you include that link to main.jsx will vary depending on the solution you chose.

For SPA users, this means your main.jsx must be linked from both the client side in HTML and the server side from C#. For a SPA, you must also link it with a pathname understandable from the client side as well as the server side, in fact you probably should also use the IncludeCSS and AddMeta methods as well. For pathnames, you must also direct the method not to use the render machine by including a "false" parameter in the IncludeScript method:
	
```
reactivePage.IncludeScript("http://yourwebsite.com/js/main.jsx",false);
reactivePage.IncludeScript("../../js/main.jsx" /*,true */); //true is a default parameter here
```

Where each consecutive "../" denotes another directory higher. So this line directs ReactJS to look two levels up from the binary folder and then into a folder labelled js.
Such that the folder structure appears as:
		
```
d/bin/debug/binary.exe
/js/main.jsx
```

This is essential, as allowing your binary folder to be accessible from your website is a clear and obvious security concern, since it's not terribly difficult to reverse engineer JIT-style binary files such as (quasi)compilations from Java and C#. Yet the client from the front-end will need access to the main.jsx script file. The alternative is doubling the file at two different locations, once for FAP.React and again for the client, but this runs the risk of desynchronising.

Again, do *not* leave your binary folder open, accessible and visible to the outside world.

### Include variations
As of v.4, there are a few ways to include scripts into your project output.
 1. `IncludeScript("Path",false)` function, with false. This will render it into the output as <script src="Path"
 2. `IncludeScript("Path")` function optionally with true. This will pass it to Babel and paste it as <sсript> Your script here
 3. `IncludeMockScript("Path")` function, which will pass it to Babel but not include it in the output anywhere
 
Which you choose is all down to your varying levels of frustration getting whatever code you're expecting a serverside rendering engine to run. You might also want to think about changing the EcmaYear and EcmaStage properties on the static ReactivePage class.

Mock scripts are your last ditch method. They ideally have only the minimal amount of code necessary to allow Babel validation.

# How do I get jQuery functions passing the Babelscript validation?

Sometimes Babel will not validate scripts with jQuery, so use a Mock version of jQuery, like this one named (Mock Jax)[https://github.com/jakerella/jquery-mockjax] , as of writing the code you must delete from this file is fairly self explanatory. Once thinned run this `IncludeMockScript("PathToMockJax.js")`

Finally...

# How to convert from jQuery built websites to ReactJS?

Whilst not strictly for FAP.React users, anyone moving to server side rendering can benefit. Any site that predominantly uses jQuery to build pages based on backend data can be converted into a JSX file ready for server side rendering. For example, 
```
$.get('api/frontpage?hereiswhereigetthefrontpagedata',function (data) {
	if(data.length > 0)
	{
		$Content = $('#Content');
		$Content.html('');
		$Content.html("<a name='Index'><div id='Title'>Index</div></a><ul>");
		for(i = 0 ; i < data.length ; i++)
		{
			post = data[i].Posts[Object.keys(data[i].Posts)[0]];
			$Content.append("<li><b><a href='#" + Object.keys(data[i].Posts)[0] + "'>" + post.Subject + "</a> on " + new Date(post.PostTime).toLocaleDateString() +"</b></li>");
		}
		$Content.append( "</ul><br><hr>");
	}
}
```
This isn't good code. By virtue of calling the same (expensive) function too often, that is the $.append() function, it will access the DOM too often and in turn take a lot longer to build. This is easily fixed.
```
$.get('api/frontpage?hereiswhereigetthefrontpagedata',function (data) {
	if(data.length > 0)
	{
		$Content = $('#Content');
		$Content.html('');
	/***From here***/
		var t = "<a name='Index'><div id='Title'>Index</div></a><ul>";
		for(i = 0 ; i < data.length ; i++)
		{
			post = data[i].Posts[Object.keys(data[i].Posts)[0]];
			t += "<li><b><a href='#" + Object.keys(data[i].Posts)[0] + "'>" + post.Subject + "</a> on " + new Date(post.PostTime).toLocaleDateString() +"</b></li>";
		}
		t+= "</ul><br><hr>";
	/***To Here***/
	//Can almost be copied and pasted
		$Content.append(t);
	}
}
```
By simply using an intermediary string "t" and then only appending once, this problem is now solve enabling us to shift to serverside rendering much easier. Instead of using Ajax to request data from the back end, we will use props. Instead of using $.append to attach the built HTML onto your page, we will use ReactJS and Babelscript.
```
var FrontPage = React.createClass({ //make sure this variable name is the same as the component name
	render() {
			var t = "<a name='Index'><div id='Title'>Index</div></a><ul>";
			for(i = 0 ; i < Object.keys(this.props).length ; i++)
			{
				post = this.props[i].Posts[Object.keys(this.props[i].Posts)[0]];
				t += "<li><b><a href='#" + Object.keys(this.props[i].Posts)[0] + "'>" + post.Subject + "</a> on " + new Date(post.PostTime).toLocaleDateString() +"</b></li>";
			}
			t+= "</ul><br><hr>";
		}
		return (
		<div>
			<a name="Index">
				<div id="Title">
					Index
				</div>
			</a>
			<div dangerouslySetInnerHTML={{__html: t}}>
			</div>
		</div>
		);
	}
});
```
Two things to note firstly, all references to "data" (what was returned from Ajax) is now this.props, with a pinch of luck this is done easily using the "Replace" function of your text editor. Furthermore, the bounds on the for loop were changed from `i < data.length` to `i < Object.keys(this.props).length` which is a small inconvenience but remember that JS and JSX are not completely the same language.

Props can be included via C# and then sourced straight from your database or through your loopback address in a pinch:
```
var reactivePage = new ReactivePage("frontpage", "FrontPage", 
JsonConvert.DeserializeObject<object>(client.DownloadString("http://127.0.0.1:9999/mydatabase")));
```
Those props will remain as the default props unless further logic is applied in the get method such as:
```
reactivePage.get = (a, b) => client.DownloadString("http://127.0.0.1:9999/mydatabase");
```
Which will ensure fresh frontpage data is accessed each time a new client connects. If you do not specify a get function, or if it does not return a JSON string, FAP.React will rely on the default props passed from the object's creation. These are default props. 

Furthermore, dangerouslySetInnerHTML is as it is: dangerously set inner HTML. It's 'dangerous' as this may (but not necessarily) open security risks if you're not careful but is still the easiest way to go from jQuery to ReactJS. Furthermore, if you'd like onClick functions to be executed without Babelfish or JSX from the front-end, you may need to use dangerouslySetInnerHTML for elements that requires these functions.

To make clearer
 1. Replace all DOM-touching statements in your jQuery code with statements that build a string
 2. Create a new main.jsx file, copy and paste that section of jQuery code in the render method
 3. Find and replace all mentions of your old Ajax data with this.props
 4. Return a dangerouslySetInnerHTML with the string you built

	And so, SEO, and by using FAP.React; you get the cake of SEO and the benefit of consuming your back end too. Enjoy.
