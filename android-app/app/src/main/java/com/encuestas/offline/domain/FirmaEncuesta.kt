package com.encuestas.offline.domain

import java.security.MessageDigest

/**
 * Firma única de encuesta (SHA256).
 *
 *   signature = SHA256( surveyId + surveyorId + timestamp + serialize(answers) )
 *   serialize(answers) = pares "clave=valor" ordenados por clave, unidos por ";"
 *
 * DEBE producir exactamente el mismo hash que [SignatureService] en la app Windows (C#).
 */
object FirmaEncuesta {

    fun serializeAnswers(answers: Map<String, String>): String =
        answers.toSortedMap().entries.joinToString(";") { "${it.key}=${it.value}" }

    fun firmar(
        surveyId: String,
        surveyorId: String,
        timestamp: String,
        answers: Map<String, String>
    ): String {
        val payload = surveyId + surveyorId + timestamp + serializeAnswers(answers)
        val digest = MessageDigest.getInstance("SHA-256")
            .digest(payload.toByteArray(Charsets.UTF_8))
        return digest.joinToString("") { "%02x".format(it) }
    }
}
