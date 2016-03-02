# FAP.React
Extension of the FAP.Page class (ReactivePage) for server side rendering with ReactJS.NET. This is an alternative network solution for ReactJS users, just assign a get function to return a JSON string

```
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
```
