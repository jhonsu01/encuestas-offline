package com.encuestas.offline.ui.theme

import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.lightColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.ui.graphics.Color

private val EncuestasColors = lightColorScheme(
    primary = Color(0xFF00695C),
    onPrimary = Color(0xFFFFFFFF),
    secondary = Color(0xFF00897B),
    onSecondary = Color(0xFFFFFFFF),
    background = Color(0xFFF5F5F5),
    surface = Color(0xFFFFFFFF)
)

@Composable
fun EncuestasTheme(content: @Composable () -> Unit) {
    MaterialTheme(colorScheme = EncuestasColors, content = content)
}
