package com.encuestas.offline.domain

import java.time.LocalTime
import java.time.format.DateTimeFormatter

/** Bloquea el uso fuera de la ventana horaria definida en la encuesta. */
object HorarioValidator {

    private val FMT: DateTimeFormatter = DateTimeFormatter.ofPattern("HH:mm")

    fun dentroDeHorario(schedule: Schedule?, now: LocalTime = LocalTime.now()): Boolean {
        val start = schedule?.startTime
        val end = schedule?.endTime
        if (start.isNullOrBlank() || end.isNullOrBlank()) return true
        return try {
            val s = LocalTime.parse(start, FMT)
            val e = LocalTime.parse(end, FMT)
            if (!s.isAfter(e)) {
                !now.isBefore(s) && !now.isAfter(e)
            } else {
                // Ventana que cruza la medianoche (p.ej. 20:00 -> 06:00)
                !now.isBefore(s) || !now.isAfter(e)
            }
        } catch (e: Exception) {
            true
        }
    }
}
