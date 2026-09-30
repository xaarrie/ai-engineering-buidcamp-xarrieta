You must migrate the examples into dotnet 10

## Nombres y localización de fichers
Los ejemplos llevaran el nombre del fichero original seguido de dotnet.
- Por ejemplo openai_v1.ipynb creará un fichero openai_v1_dotnet.cs
Los ficheros estarán en la misma carpeta pero dentro de la seccion dotnet
- Por ejemplo 01-Foundation/02-RAG/openai_v1.ipybn generara el fichero cs en
dotnet/01-Foundation/02-RAG/openai_v1_dotnet.cs

## Requisitos dotnet
Se usaran file based apps para los ficheros siendo cada fichero independiente sin un csproj
Se ejecuta con dotnet run ejemplo01.cs

Deben usarse las variables de entorno definidas en el fichero .env de la raiz del repositorio.

Para todo el tema de operaciones de IA y hablar con modelos se debe utilizar
Microsoft.Agent.Framework en su versión mas actual.

## Generación del ejemplo
Debe entenderse los pasos de jupyter notebooks y lo que pretenden mostrar
Se debe implementar la misma funcionalidad usando Microsoft Agent Framework.
Es decir si hacemos una peticion y se muestran despues en consumo de tokens, lo mismo debe implementarse en dotnet.
Debe intentar ser simple, son ejemplos demostrativos con el fin de enseñar el manejo del framework
Al ser algo formativo debes comentar las secciones de codigo como mínimo con como estan comentadas en jupyter notebooks, puedes añadir comentarios donde consideres necesario.

## Herramientas de python no disponibles en dotnet


