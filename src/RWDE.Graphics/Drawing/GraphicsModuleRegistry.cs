using System;
using System.Collections.Generic;
using RWDE.Sim;

namespace RWDE.Graphics
{
    /// <summary>
    /// Which graphics module draws which sim class: our stand-in for the game's
    /// <c>PhysicalObject.InitiateGraphicsModule</c> overrides, which the sim cannot have because it never
    /// references graphics. The most derived registered type wins.
    /// </summary>
    public sealed class GraphicsModuleRegistry
    {
        private readonly Dictionary<Type, Func<PhysicalObject, GraphicsModule>> _factories =
            new Dictionary<Type, Func<PhysicalObject, GraphicsModule>>();

        public void Register<T>(Func<T, GraphicsModule> create) where T : PhysicalObject
        {
            if (create == null) throw new ArgumentNullException(nameof(create));
            _factories[typeof(T)] = obj => create((T)obj);
        }

        /// <summary>A new module for <paramref name="obj"/>, or null when nothing is registered for its class.</summary>
        public GraphicsModule Create(PhysicalObject obj)
        {
            for (var type = obj.GetType(); type != null && type != typeof(object); type = type.BaseType)
            {
                if (_factories.TryGetValue(type, out var create)) return create(obj);
            }
            return null;
        }
    }
}
