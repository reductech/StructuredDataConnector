﻿using System.Linq;
using System.Xml.Linq;

namespace Reductech.Sequence.Connectors.StructuredData;

/// <summary>
/// Extracts the entity from a Xml stream containing a single entity.
/// </summary>
public sealed class FromXml : CompoundStep<Entity>
{
    /// <inheritdoc />
    protected override async Task<Result<Entity, IError>> Run(
        IStateMonad stateMonad,
        CancellationToken cancellationToken)
    {
        var stream = await Stream.Run(stateMonad, cancellationToken);

        if (stream.IsFailure)
            return stream.ConvertFailure<Entity>();

        XElement element;

        try
        {
            element = await XElement.LoadAsync(
                stream.Value.GetStream().stream,
                LoadOptions.None,
                cancellationToken
            );
        }
        catch
        {
            return
                Result.Failure<Entity, IError>(
                    ErrorCode.CouldNotParse.ToErrorBuilder(stream.Value, "XML")
                        .WithLocation(this)
                );
        }

        var sclObject = ToSCLObject(element);

        if (sclObject is Entity entity)
            return entity;

        var result = Entity.Create((Entity.PrimitiveKey, sclObject));
        return result;
    }

    /// <summary>
    /// Stream containing the Xml data.
    /// </summary>
    [StepProperty(1)]
    [Required]
    public IStep<StringStream> Stream { get; set; } = null!;

    /// <inheritdoc />
    public override IStepFactory StepFactory { get; } =
        new SimpleStepFactory<FromXml, Entity>();

    private static ISCLObject ToSCLObject(XElement element)
    {
        if (element.IsEmpty)
            return SCLNull.Instance;

        if (element.HasElements)
        {
            var l = new List<EntityProperty>();

            //this is an entity
            foreach (var group in element.Elements().GroupBy(x => x.Name))
            {
                if (group.Count() == 1)
                {
                    var value = ToSCLObject(group.Single());
                    l.Add(new EntityProperty(group.Key.LocalName, value, l.Count));
                }
                else
                {
                    var array = group.Select(ToSCLObject).ToSCLArray();
                    l.Add(new EntityProperty(group.Key.LocalName, array, l.Count));
                }
            }

            var entity = new Entity(l);
            return entity;
        }
        else
        {
            return new StringStream(element.Value);
        }
    }
}
