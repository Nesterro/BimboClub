using System;
using Autodesk.Revit.DB;

namespace BimboClub
{
    internal sealed class SchemeParameterReader
    {
        private readonly Document _document;
        private readonly ElementId _parameterId;
        private readonly BuiltInParameter? _builtIn;
        private readonly Definition _definition;

        internal SchemeParameterReader(Document document, ElementId parameterId)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            _parameterId = parameterId ?? throw new ArgumentNullException(nameof(parameterId));
            if (parameterId.Equals(ElementId.InvalidElementId))
                throw new ArgumentException("Не выбран параметр фильтрации.", nameof(parameterId));

            if (parameterId.IntegerValue < 0)
                _builtIn = (BuiltInParameter)parameterId.IntegerValue;
            else
            {
                _definition = (document.GetElement(parameterId) as ParameterElement)?.GetDefinition();
                if (_definition == null)
                    throw new ArgumentException("Параметр фильтрации не найден в документе.", nameof(parameterId));
            }
        }

        internal string Read(Element element)
        {
            if (element == null) return null;
            try
            {
                string value = ReadOwnParameter(element);
                if (value != null) return value; // An explicitly empty string is not a missing value.

                ElementId typeId = element.GetTypeId();
                if (typeId == null || typeId.Equals(ElementId.InvalidElementId) || typeId.Equals(element.Id))
                    return null;
                Element type = _document.GetElement(typeId);
                return type == null ? null : ReadOwnParameter(type);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Не удалось прочитать параметр {_parameterId} у элемента {element.Id} при создании схемы. {ex.Message}", ex);
            }
        }

        private string ReadOwnParameter(Element element)
        {
            // Read through Parameter, not FilterableValueProvider.GetStringValue:
            // the latter raised an internal Revit error on the user's model.
            Parameter parameter = _builtIn.HasValue
                ? element.get_Parameter(_builtIn.Value)
                : element.get_Parameter(_definition);
            if (parameter == null || parameter.StorageType != StorageType.String || !parameter.HasValue)
                return null;
            return parameter.AsString();
        }
    }
}
