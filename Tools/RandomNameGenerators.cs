namespace Hubcon.Tools
{
    using System;

    public static class RandomNameGenerator
    {
        private static Random random = new Random();

        // Lista de posibles nombres
        private static string[] possibleNames = { "Alice", "Bob", "Charlie", "David", "Eve", "Frank", "Grace", "Heidi", "Ivy", "Jack", "Kate", "Liam", "Mia", "Noah", "Olivia", "Paul", "Quinn", "Ryan", "Sara", "Tom", "Uma", "Victor", "Wendy", "Xavier", "Yara", "Zane" };

        public static string GenerateRandomName()
        {
            // Obtener un nombre aleatorio de la lista de posibles nombres
            string randomName = possibleNames[random.Next(possibleNames.Length)];

            // Obtener un número aleatorio entre 1 y 100
            int randomNumber = random.Next(1, 320629); // El rango es [1, 100]

            // Combinar el nombre aleatorio y el número aleatorio en una cadena
            string randomFullName = $"{randomName}{randomNumber}";

            return randomFullName;
        }
    }
}
