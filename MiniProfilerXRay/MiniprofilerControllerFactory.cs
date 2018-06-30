using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Internal;
using StackExchange.Profiling;

namespace MiniProfilerXRay
{
    public class MiniprofilerControllerFactory : IControllerFactory
    {
        private readonly IControllerFactory _factory;

        public MiniprofilerControllerFactory(IControllerActivator activator, IEnumerable<IControllerPropertyActivator> propertyActivators)
        {
            _factory = new DefaultControllerFactory(activator, propertyActivators);
        }

        public object CreateController(ControllerContext context)
        {
            using (MiniProfiler.Current.Step("Controller Create: " + context.ActionDescriptor.ControllerName))
            {
                return _factory.CreateController(context);
            }
        }

        public void ReleaseController(ControllerContext context, object controller)
        {
            _factory.ReleaseController(context, controller);
        }
    }
}