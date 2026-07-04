package com.encuestas.offline.domain

/** Valida las respuestas contra el formulario. Devuelve la lista de errores (vacía = OK). */
object FormValidator {

    fun validar(
        questions: List<Question>,
        answers: Map<String, String>,
        hasImage: Boolean,
        hasGps: Boolean
    ): List<String> {
        val errors = mutableListOf<String>()
        for (q in questions) {
            val value = answers[q.id]?.trim().orEmpty()
            when (q.type) {
                "image" -> if (q.required && !hasImage) errors += "Falta la imagen: ${q.label}"
                "gps" -> if (q.required && !hasGps) errors += "Falta la ubicación GPS: ${q.label}"
                "number" -> {
                    if (q.required && value.isEmpty()) {
                        errors += "Falta: ${q.label}"
                    } else if (value.isNotEmpty()) {
                        val n = value.toIntOrNull()
                        if (n == null) {
                            errors += "'${q.label}' debe ser numérico"
                        } else {
                            if (q.min != null && n < q.min) errors += "'${q.label}': mínimo ${q.min}"
                            if (q.max != null && n > q.max) errors += "'${q.label}': máximo ${q.max}"
                        }
                    }
                }
                else -> if (q.required && value.isEmpty()) errors += "Falta: ${q.label}"
            }
        }
        return errors
    }
}
