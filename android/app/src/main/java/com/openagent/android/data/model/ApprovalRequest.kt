package com.openagent.android.data.model

/**
 * One pending approval pushed by the host (docs/protocol.md §4.4, artboards
 * 11/12): the same card the host's screen shows, answerable from the phone.
 */
data class ApprovalRequest(
    val id: String,
    val taskId: String,
    val tool: String,
    val title: String,
    val args: String,
    /** The host's RiskLevel enum name, e.g. "Medium". */
    val risk: String,
    val reversible: Boolean,
    val expiresAt: Long,
) {
    val riskLabel: String
        get() = when (risk) {
            "Safe" -> "安全"
            "Low" -> "低风险"
            "Medium" -> "中风险"
            "High" -> "高风险"
            "Critical" -> "严重"
            else -> risk
        }
}
