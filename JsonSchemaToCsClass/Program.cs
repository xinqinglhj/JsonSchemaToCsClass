﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JsonSchemaToCsClass
{
    class Program
    {
        static void Main(string[] args)
        {
            var schema = JsonSchema.Load("basic.json");

            var generator = new CsClassGenerator();
            generator.ParseSchema(schema);

            var option = new ClassConstructionOptions()
            {
                Namespace = "Hoge.Foo",
                //IsJsonSerializable = true,
            };
            generator.ConstructDeclaration(option);
            Console.WriteLine(generator.ToFullString());

            Console.WriteLine("=============================");

            option.IsJsonSerializable = true;
            generator.ConstructDeclaration(option);
            Console.WriteLine(generator.ToFullString());

            Console.Read();
        }
    }
}