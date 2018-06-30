# MiniProfilerXRay
a simple storage provider for MiniProfiler .NET that publishes traces to Amazon X-Ray


## NOTE: Work in Progress. Use at your own risk. 

## Examples 
### Simple Console App

![alt text](https://github.com/rudygt/MiniProfilerXRay/blob/master/MiniProfilerXRay.ConsoleSample/XRayTraceScreenshot.PNG "X-Ray Console")

### Original Mini Profiler Console Sample

![alt text](https://github.com/rudygt/MiniProfilerXRay/blob/master/Samples.ConsoleCore/XRayTraceConsole.PNG "Text View")

![alt text](https://github.com/rudygt/MiniProfilerXRay/blob/master/Samples.ConsoleCore/XRayTraceTest.PNG "X-Ray Console View")

### Original Mini Profiler Console Multi Thread Sample

![alt text](https://github.com/rudygt/MiniProfilerXRay/blob/master/Samples.ConsoleCore/XRayTraceConsoleMultiThread.PNG "Text View")

![alt text](https://github.com/rudygt/MiniProfilerXRay/blob/master/Samples.ConsoleCore/XRayTraceTestMultiThread.PNG "X-Ray Console View")

### Original Mini Profiler AspNetcore MVC Sample

EntityFrameworkCore trace

![alt text](https://github.com/rudygt/MiniProfilerXRay/blob/master/Samples.AspNetCore/EntityFrameworkCoreTestXWebSS.PNG "Render View")

![alt text](https://github.com/rudygt/MiniProfilerXRay/blob/master/Samples.AspNetCore/EntityFrameworkCoreTestXRayTrace.PNG "X-Ray Console View")

ParametrizedSqlWithEnums trace 

![alt text](https://github.com/rudygt/MiniProfilerXRay/blob/master/Samples.AspNetCore/ParametrizedSqlWithEnumsWebSS.PNG "Render View")

![alt text](https://github.com/rudygt/MiniProfilerXRay/blob/master/Samples.AspNetCore/ParametrizedSqlWithEnums.PNG "X-Ray Console View")

Extra this package includes a custom IControllerFactory that allows Mini Profiler to trace controller creation time

![alt text](https://github.com/rudygt/MiniProfilerXRay/blob/master/Samples.AspNetCore/ProfileControllerCreation.PNG "X-Ray Console View")
