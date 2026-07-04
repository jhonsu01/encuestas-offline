package com.encuestas.offline.domain

/** Formulario de encuesta (contrato del shared-schema). */
data class Survey(
    val id: String = "",
    val version: Int = 1,
    val title: String = "",
    val description: String? = null,
    val schedule: Schedule? = null,
    val questions: List<Question> = emptyList()
)

data class Schedule(
    val startTime: String? = null,
    val endTime: String? = null,
    val timezone: String? = null
)

data class Question(
    val id: String = "",
    val type: String = "text",
    val label: String = "",
    val required: Boolean = false,
    val options: List<String>? = null,
    val min: Int? = null,
    val max: Int? = null
)

enum class DocumentType { CC, CE }

/** Encuestador registrado en el dispositivo. */
data class Surveyor(
    val documentType: DocumentType,
    val documentNumber: String,
    val fullName: String,
    val pin: String
) {
    /** Identificador estable: p.ej. "CC-1020304050". */
    val id: String get() = "${documentType.name}-$documentNumber"
}
