using System;
using System.Reflection;
using UnityEditor.Callbacks;
using System.Collections;

namespace NKStudio
{
    public static class DisableUFolderGizmo
    {
        [DidReloadScripts]
        private static void OnScriptsReloaded()
        {
            if (!TryGetType("UnityEditor.AnnotationUtility, UnityEditor", out Type annotationUtilityType))
                return;

            if (!TryGetStaticMethod(annotationUtilityType, "GetAnnotations", out MethodInfo getAnnotationsMethod))
                return;

            if (getAnnotationsMethod.Invoke(null, null) is not IEnumerable annotations)
                return;

            if (!TryGetType("UnityEditor.Annotation, UnityEditor", out Type annotationType))
                return;

            if (!TryGetField(annotationType, "scriptClass", out FieldInfo scriptClassField))
                return;

            foreach(object a in annotations)
            {
                string className = scriptClassField.GetValue(a) as string;

                if(!string.Equals(className, "UFolder"))
                    continue;

                if(!TryGetField(annotationType, "flags", out FieldInfo flagsField))
                    return;
                
                int flags = (int)flagsField.GetValue(a);
                bool hasIcon = (flags & 1) == 1;
                
                if(!hasIcon)
                    return;

                if(!TryGetField(annotationType, "classID", out FieldInfo classIdField))
                    return;
                
                int classId = (int)classIdField.GetValue(a);

                if(!TryGetField(annotationType, "iconEnabled", out FieldInfo iconEnabledField))
                    return;
                
                int iconEnabled = (int)iconEnabledField.GetValue(a);
                
                if(iconEnabled == 0)
                    return;

                if(!TryGetStaticMethod(annotationUtilityType, "SetIconEnabled", out MethodInfo setIconEnabledMethod))
                    return;
                
                setIconEnabledMethod.Invoke(null, new object[] { classId, className, 0 });
                return;
            }
        }

        /// <summary>
        /// Attempts to retrieve a Type object based on the specified type name.
        /// </summary>
        /// <param name="typeName">The name of the type to retrieve.</param>
        /// <param name="result">When this method returns, contains the Type object
        /// associated with the specified type name, if the type is found;
        /// otherwise, null. This parameter is passed uninitialized.</param>
        /// <returns>true if the Type object is successfully retrieved;
        /// otherwise, false.</returns>
        private static bool TryGetType(string typeName, out Type result)
        {
            result = Type.GetType(typeName, false);
            return result != null;
        }

        /// <summary>
        /// Tries to retrieve a field with the given name from a specified type.
        /// </summary>
        /// <param name="type">The type to search for the field in.</param>
        /// <param name="fieldName">The name of the field to retrieve.</param>
        /// <param name="result">When this method returns, contains the FieldInfo object representing the retrieved field, if found; otherwise, null.</param>
        /// <returns>true if the field is found; otherwise, false.</returns>
        private static bool TryGetField(Type type, string fieldName, out FieldInfo result)
        {
            result = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return result != null;
        }

        /// <summary>
        /// Tries to get a static method from the specified type by name.
        /// </summary>
        /// <param name="type">The type to search for the method in.</param>
        /// <param name="methodName">The name of the method.</param>
        /// <param name="result">The output parameter that will hold the method information if found.</param>
        /// <returns>Returns a boolean indicating whether the method was found or not.</returns>
        private static bool TryGetStaticMethod(Type type, string methodName, out MethodInfo result)
        {
            result = type.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
            return result != null;
        }
    }
}
