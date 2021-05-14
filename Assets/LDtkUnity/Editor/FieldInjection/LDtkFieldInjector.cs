﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace LDtkUnity.Editor
{
    public class LDtkFieldInjector
    {
        private readonly GameObject _instance;
        private readonly FieldInstance[] _fieldInstances;
        private readonly List<InjectorDataPair> _injectorData = new List<InjectorDataPair>();
        
        public InjectorDataPair[] InjectorData => _injectorData.ToArray();

        public LDtkFieldInjector(GameObject instance, FieldInstance[] fieldInstances)
        {
            _instance = instance;
            _fieldInstances = fieldInstances;
        }

        public void InjectEntityFields()
        {
            if (_fieldInstances.IsNullOrEmpty())
            {
                return;
            }
            
            LDtkInjectionErrorContext.SetLogErrorContext(_instance);

            MonoBehaviour[] monoBehaviours = _instance.GetComponents<MonoBehaviour>();
            List<LDtkFieldInjectorData> injectableFields = monoBehaviours.SelectMany(GetAttributeFieldsFromComponent).ToList();
            
            //validation. don't use for now and see if it's easier to keep track of errors
            //CheckFieldDefinitionsExistence(_fieldInstances.Select(p => p.Identifier).ToList(), injectableFields.Select(p => p.FieldIdentifier).ToList());
            
            //run though all of the LDtk variables as the main proprietor.
            InjectAllFieldsIntoInstance(injectableFields);
            
        }
        
        private List<LDtkFieldInjectorData> GetAttributeFieldsFromComponent(MonoBehaviour component)
        {
            return (from fieldInfo in component.GetType().GetFields() 
                
                let attribute = fieldInfo.GetCustomAttribute<LDtkFieldAttribute>() 
                where attribute != null
                let fieldName = attribute.IsCustomDefinedName ? attribute.DataIdentifier : fieldInfo.Name
                
                select new LDtkFieldInjectorData(fieldInfo, fieldName, component)).ToList();
        }

        private void InjectAllFieldsIntoInstance(List<LDtkFieldInjectorData> injectableFields)
        {
            foreach (FieldInstance fieldData in _fieldInstances)
            {
                LDtkFieldInjectorData fieldToInjectInto = injectableFields.FirstOrDefault(injectableField => injectableField.FieldIdentifier == fieldData.Identifier);
                
                if (fieldToInjectInto == null)
                {
                    Debug.LogError($"LDtk: LDtk field '{fieldData.Type}' \"{fieldData.Identifier}\" could not find a matching C# field to inject into. Is the field not public?", LDtkInjectionErrorContext.Context);
                    continue;
                }

                InjectorDataPair pair = new InjectorDataPair(fieldData, fieldToInjectInto);
                _injectorData.Add(pair);
                
                InjectFieldIntoInstance(fieldData, fieldToInjectInto);
            }
        }

        private void InjectFieldIntoInstance(FieldInstance fieldInstance, LDtkFieldInjectorData fieldToInjectInto)
        {
            if (fieldInstance.Type.Contains("Array"))
            {
                //validate that the field is an array
                if (!fieldToInjectInto.Info.FieldType.IsArray)
                {
                    Debug.LogError($"LDtk: The LDtk field \"{fieldInstance.Identifier}\" is an array but the C# field is not.");
                    return;
                }

                InjectArray(fieldInstance, fieldToInjectInto);
                return;
            }

            //validate that the field is NOT an array
            if (fieldToInjectInto.Info.FieldType.IsArray)
            {
                Debug.LogError($"LDtk: The LDtk field \"{fieldInstance.Identifier}\" is not an array but the C# is.");
                return;
            }
            
            InjectSingle(fieldInstance, fieldToInjectInto);
            
        }
        
        private void InjectArray(FieldInstance fieldInstance, LDtkFieldInjectorData fieldToInjectInto)
        {
            Type elementType = fieldToInjectInto.Info.FieldType.GetElementType();
            if (elementType == null)
            {
                throw new InvalidOperationException();
            }

            object[] values = ((IEnumerable) fieldInstance.Value).Cast<object>()
                .Select(x => x == null ? (object)null : x.ToString()).ToArray();
            
            object[] objs = values.Select(value => GetParsedValue(fieldInstance.Type, value, elementType)).ToArray();
            
            Array array = Array.CreateInstance(elementType, objs.Length);
            Array.Copy(objs, array, objs.Length);

            fieldToInjectInto.SetField(array);
        }
        private void InjectSingle(FieldInstance fieldInstance, LDtkFieldInjectorData fieldToInjectInto)
        {
            Type type = fieldToInjectInto.Info.FieldType;
            object field = GetParsedValue(fieldInstance.Type, fieldInstance.Value, type);
            fieldToInjectInto.SetField(field);
        }
        
        private object GetParsedValue(string fieldInstanceType, object value, Type type)
        {
            ParseFieldValueAction action;
            if (type.IsEnum || type.IsArray && type.GetElementType().IsEnum)
            {
                action = LDtkFieldParser.GetEnumMethod(type);
            }
            else
            {
                action = LDtkFieldParser.GetParserMethodForType(fieldInstanceType);
            }
            
            return action?.Invoke(value);
        }
       
        private void CheckFieldDefinitionsExistence( 
            ICollection<string> fieldsData,
            ICollection<string> fieldInfos)
        {
            foreach (string fieldData in fieldsData.Where(fieldData => !fieldInfos.Contains(fieldData)))
            {
                Debug.LogError($"LDtk: LDtk Field \"{fieldData}\" is defined but does not have a matching C# field. Misspelled or missing attribute?", LDtkInjectionErrorContext.Context);
            }

            foreach (string fieldInfo in fieldInfos.Where(fieldInfo => !fieldsData.Contains(fieldInfo)))
            {
                Debug.LogError($"LDtk: C# field \"{fieldInfo}\" uses [LDtkField] but does not have a matching LDtk field. Misspelled, undefined in LDtk editor, or unnessesary attribute?", LDtkInjectionErrorContext.Context);
            }
        }
    }
}