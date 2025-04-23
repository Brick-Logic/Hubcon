using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.GraphQL.Data
{
    public  class Data
    {
        public static List<TestData> TestingData { get; set; } = new()
        {
            new TestData(1, "nombre1", "descripcion1", "tipo1", "tipodescripcion1"),
            new TestData(2, "nombre2", "descripcion2", "tipo2", "tipodescripcion2"),
            new TestData(3, "nombre3", "descripcion3", "tipo3", "tipodescripcion3"),
            new TestData(4, "nombre4", "descripcion4", "tipo4", "tipodescripcion4"),
            new TestData(5, "nombre5", "descripcion5", "tipo5", "tipodescripcion5"),
        };
    }

    public class TestData
    {
        public TestData()
        {
        }

        public TestData(int id, string name, string description, string type, string typeDescription)
        {
            Id = id;
            Name = name;
            Description = description;
            Type = type;
            TypeDescription = typeDescription;
        }

        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string TypeDescription { get; set; } = string.Empty;
    }
}
