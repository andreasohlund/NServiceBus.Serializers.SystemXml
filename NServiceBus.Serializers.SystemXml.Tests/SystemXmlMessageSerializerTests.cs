﻿namespace NServiceBus.Serializers.SystemXml.Tests
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Xml.Linq;
    using System.Xml.Serialization;
    using Xunit;

    public class SystemXmlMessageSerializerTests
    {
        [Fact]
        public void ItShouldHaveAProperContentType()
        {
            Assert.Equal("text/xml", new SystemXmlMessageSerializer().ContentType);
        }

        [Fact]
        public void ItShouldThrowAnExceptionIfNoMessagesSpecified()
        {
            Assert.Throws<ArgumentException>(() => SerializeMessagesWithoutWrapper(new object[] {}));
        }


        [Fact]
        public void ItShouldSerializeSingleObjectWithoutWrapper()
        {
            var unwrappedMessage = SerializeMessagesWithoutWrapper(new object[] {DateTime.MinValue});
            Assert.Contains(@"<dateTime>0001-01-01T00:00:00</dateTime>", unwrappedMessage);
            Assert.DoesNotContain(SystemXmlMessageSerializer.EnvelopeName, unwrappedMessage);
        }

        [Fact]
        public void ItShouldSerializeSingleObjectWithWrapper()
        {
            var unwrappedMessage = SerializeMessagesWithWrapper(new object[] { DateTime.MinValue });
            Assert.Contains(@"<dateTime>0001-01-01T00:00:00</dateTime>", unwrappedMessage);
            Assert.True(unwrappedMessage.StartsWith("<?xml"));
            Assert.Contains(SystemXmlMessageSerializer.EnvelopeName, unwrappedMessage);
        }

        [Fact]
        public void ItShouldSerializeMultipleObjectsIntoContainer()
        {
            var serializedMessage = SerializeMessagesWithoutWrapper(new object[] {DateTime.MinValue, DateTime.MaxValue});
            var x = XDocument.Parse(serializedMessage);
            Assert.Equal(SystemXmlMessageSerializer.EnvelopeName, x.Root.Name);
            Assert.Equal(2, x.Root.Elements().Count());
        }

        [Fact]
        public void ItShouldHonorSystemXmlAnnotationsOnSerialize()
        {
            var ser = SerializeMessagesWithoutWrapper(new object[] {new Foo{PersonName = "Phil", Years = 15}});
            var x = XDocument.Parse(ser);
            Assert.Equal("Person", x.Root.Name);
            Assert.Equal(1, x.Root.Elements().Count());
            var firstChild = x.Root.Elements().First();
            Assert.Equal("Age", firstChild.Name);
            Assert.Equal("15", firstChild.Value);
            Assert.Equal(1, x.Root.Attributes("Name").Count());
            Assert.Equal("Phil", x.Root.Attributes("Name").First().Value);
        }

        [Fact]
        public void ItShouldBeAbleToDeserializeDateProperly()
        {
            var objs = DeserializeXML(@"<?xml version=""1.0"" ?><dateTime>0001-01-01T00:00:00</dateTime>", new[]{typeof(DateTime)});
            Assert.Equal(1, objs.Length);
            var obj = objs.First();
            Assert.IsType<DateTime>(obj);
        }

        [Fact]
        public void ItShouldBeAbleToDeserializeArbitraryTypes()
        {
            var objs = DeserializeXML(@"<?xml version=""1.0"" ?><Person Name=""Bob""><Age>15</Age></Person>", new[] { typeof(Foo) });
            Assert.Equal(1, objs.Length);
            var obj = objs.First();
            Assert.IsType<Foo>(obj);
            var foo = obj as Foo;
            Assert.Equal(15, foo.Years);
            Assert.Equal("Bob", foo.PersonName);
        }

        [Fact]
        public void ItShouldBeAbleToDeserializeArbitraryTypesWithMissingTypeNamesForNestedObjects()
        {
            var objs = DeserializeXML(@"<?xml version=""1.0"" ?><Bar><Foo Name=""Bob""><Age>15</Age></Foo></Bar>", new[] { typeof(Bar) });
            Assert.Equal(1, objs.Length);
            var obj = objs.First();
            Assert.IsType<Bar>(obj);
            var foo = obj as Bar;
            Assert.Equal(15, foo.Foo.Years);
            Assert.Equal("Bob", foo.Foo.PersonName);
        }

        [Fact]
        public void ItShouldBeAbleToDeserializeSingleObjectInWrapper()
        {
            var objs = DeserializeXML(@"<?xml version=""1.0"" ?><Messages><MyClass xmlns='MyNamespace'><Message>Hello</Message></MyClass></Messages>", new[] { typeof(MyClass) });
            Assert.Equal(1, objs.Length);
            var obj = objs.First();
            Assert.IsType<MyClass>(obj);
            var foo = obj as MyClass;
            Assert.Equal("Hello", foo.Message);
        }


        [Fact]
        public void ItShouldFailIfNoTypesAreSpecified()
        {
            Assert.Throws<ArgumentException>(() => DeserializeXML(@"<?xml version=""1.0"" ?><Person Name=""Bob""><Age>15</Age></Person>", null));
        }


        private object[] DeserializeXML(string message, Type[] types)
        {
            var ser = new SystemXmlMessageSerializer();
            using (var stream = new MemoryStream())
            {
                var bytes = Encoding.UTF8.GetBytes(message.ToCharArray());
                stream.Write(bytes, 0, bytes.Length);
                stream.Seek(0, SeekOrigin.Begin);
                stream.Flush();
                return ser.Deserialize(stream, types);
            }
        }

        [XmlRoot(ElementName = "Bar", DataType = "bar")]
        public class Bar
        {
            [XmlElement(ElementName = "Foo")]
            public Foo Foo { get; set; }
        }
        [XmlRoot(ElementName = "Person", DataType = "bob")]
        public class Foo
        {
            [XmlAttribute(AttributeName = "Name")]
            public string PersonName { get; set; }
            [XmlElement(ElementName = "Age")]
            public int Years { get; set; }
        }

        [XmlRoot(Namespace = "MyNamespace")]
        public class MyClass
        {
            public string Message { get; set; }
        }
        private static string SerializeMessagesWithoutWrapper(object[] messages)
        {
            return SerializeMessages(messages, true);
        }
        private static string SerializeMessagesWithWrapper(object[] messages)
        {
            return SerializeMessages(messages, false);
        }
        private static string SerializeMessages(object[] messages, bool skipWrappingElementForSingleMessages)
        {
            string s;
            var ser = new SystemXmlMessageSerializer { SkipWrappingElementForSingleMessages = skipWrappingElementForSingleMessages };
            using (var stream = new MemoryStream())
            {
                ser.Serialize(messages, stream);
                stream.Flush();
                stream.Seek(0, SeekOrigin.Begin);
                using (var streamReader = new StreamReader(stream))
                {
                    s = streamReader.ReadToEnd();
                }
            }
            return s;
        }
    }
}
