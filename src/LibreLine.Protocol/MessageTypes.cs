// SPDX-License-Identifier: GPL-2.0-only
using System.Collections.Frozen;
using Google.Protobuf;
using Google.Protobuf.Reflection;

namespace LibreLine.Protocol;

/// <summary>
/// The message catalogue: each message type is an extension of <c>com.daysofwonder.mm.Message</c>, and its field
/// number is the message's type number. Built from the generated descriptors, so it can't drift from the protos.
/// </summary>
public static class MessageTypes
{
    private const string EnvelopeMessage = "com.daysofwonder.mm.Message";

    private static readonly FrozenDictionary<Type, int> Numbers;
    private static readonly FrozenDictionary<int, MessageDescriptor> Descriptors;

    static MessageTypes()
    {
        var numbers = new Dictionary<Type, int>();
        var descriptors = new Dictionary<int, MessageDescriptor>();
        var files = new[]
        {
            Com.Daysofwonder.Async.AsyncReflection.Descriptor,
            Com.Daysofwonder.Mm.RequestReflection.Descriptor,
        };

        foreach (var extension in files.SelectMany(AllExtensions))
        {
            if (extension.ExtendeeType.FullName != EnvelopeMessage || extension.MessageType is null)
            {
                continue;
            }

            var type = extension.MessageType.ClrType;
            var number = extension.FieldNumber;

            // Ping (777) is defined once, in the older request protocol, and is shared by both.
            numbers.TryAdd(type, number);
            descriptors.TryAdd(number, extension.MessageType);
        }

        Numbers = numbers.ToFrozenDictionary();
        Descriptors = descriptors.ToFrozenDictionary();
    }

    public const int Ping = 777;

    /// <summary>The type number of a message class.</summary>
    public static int NumberOf(Type type) =>
        Numbers.TryGetValue(type, out var number)
            ? number
            : throw new ArgumentException($"{type.Name} is not a message of the envelope", nameof(type));

    public static int NumberOf<T>() where T : IMessage => NumberOf(typeof(T));

    public static bool TryGetDescriptor(int number, out MessageDescriptor descriptor) =>
        Descriptors.TryGetValue(number, out descriptor!);

    /// <summary>A short name for logs, e.g. <c>AsyncAuth (400)</c>.</summary>
    public static string Describe(int number) =>
        Descriptors.TryGetValue(number, out var d)
            ? $"{(d.Name.EndsWith("Request", StringComparison.Ordinal) ? d.Name[..^7] : d.Name)} ({number})"
            : $"unknown ({number})";

    private static IEnumerable<FieldDescriptor> AllExtensions(FileDescriptor file) =>
        file.Extensions.UnorderedExtensions.Concat(file.MessageTypes.SelectMany(AllExtensions));

    private static IEnumerable<FieldDescriptor> AllExtensions(MessageDescriptor message) =>
        message.Extensions.UnorderedExtensions.Concat(message.NestedTypes.SelectMany(AllExtensions));
}
