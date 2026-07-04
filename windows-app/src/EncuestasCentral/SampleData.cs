namespace EncuestasCentral;

public static class SampleData
{
    public const string ExampleJson = """
{
  "id": "survey-demo-001",
  "version": 1,
  "title": "Encuesta Socioeconómica Demo",
  "description": "Formulario de ejemplo para demostrar el motor dinámico.",
  "schedule": { "startTime": "06:00", "endTime": "20:00", "timezone": "America/Bogota" },
  "questions": [
    { "id": "q1", "type": "text", "label": "Nombre completo del encuestado", "required": true },
    { "id": "q2", "type": "single_choice", "label": "Estrato socioeconómico", "required": true, "options": ["1","2","3","4","5","6"] },
    { "id": "q3", "type": "multiple_choice", "label": "Servicios públicos disponibles", "required": false, "options": ["Agua","Luz","Gas","Internet","Alcantarillado"] },
    { "id": "q4", "type": "number", "label": "Número de personas en el hogar", "required": true, "min": 1, "max": 30 },
    { "id": "q5", "type": "date", "label": "Fecha de nacimiento del jefe de hogar", "required": true },
    { "id": "q6", "type": "image", "label": "Foto de la fachada de la vivienda", "required": true },
    { "id": "q7", "type": "gps", "label": "Ubicación de la vivienda", "required": true }
  ]
}
""";
}
