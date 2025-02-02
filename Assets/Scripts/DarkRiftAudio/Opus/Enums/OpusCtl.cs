﻿namespace DarkRiftAudio
{
    public enum OpusCtl
    {
        SET_APPLICATION_REQUEST = 4000,
        GET_APPLICATION_REQUEST = 4001,
        SET_BITRATE_REQUEST = 4002,
        GET_BITRATE_REQUEST = 4003,
        SET_MAX_BANDWIDTH_REQUEST = 4004,
        GET_MAX_BANDWIDTH_REQUEST = 4005,
        SET_VBR_REQUEST = 4006,
        GET_VBR_REQUEST = 4007,
        SET_BANDWIDTH_REQUEST = 4008,
        GET_BANDWIDTH_REQUEST = 4009,
        SET_COMPLEXITY_REQUEST = 4010,
        GET_COMPLEXITY_REQUEST = 4011,
        SET_INBAND_FEC_REQUEST = 4012,
        GET_INBAND_FEC_REQUEST = 4013,
        SET_PACKET_LOSS_PERC_REQUEST = 4014,
        GET_PACKET_LOSS_PERC_REQUEST = 4015,
        SET_DTX_REQUEST = 4016,
        GET_DTX_REQUEST = 4017,
        SET_VBR_CONSTRAINT_REQUEST = 4020,
        GET_VBR_CONSTRAINT_REQUEST = 4021,
        SET_FORCE_CHANNELS_REQUEST = 4022,
        GET_FORCE_CHANNELS_REQUEST = 4023,
        SET_SIGNAL_REQUEST = 4024,
        GET_SIGNAL_REQUEST = 4025,
        GET_LOOKAHEAD_REQUEST = 4027,
        RESET_STATE = 4028,
        GET_SAMPLE_RATE_REQUEST = 4029,
        GET_FINAL_RANGE_REQUEST = 4031,
        GET_PITCH_REQUEST = 4033,
        SET_GAIN_REQUEST = 4034,
        GET_GAIN_REQUEST = 4045 /* Should have been 4035 */,
        SET_LSB_DEPTH_REQUEST = 4036,
        GET_LSB_DEPTH_REQUEST = 4037,
        GET_LAST_PACKET_DURATION_REQUEST = 4039,
        SET_EXPERT_FRAME_DURATION_REQUEST = 4040,
        GET_EXPERT_FRAME_DURATION_REQUEST = 4041,
        SET_PREDICTION_DISABLED_REQUEST = 4042,
        GET_PREDICTION_DISABLED_REQUEST = 4043,
/* Don't use 4045, it's already taken by OPUS_GET_GAIN_REQUEST */
        SET_PHASE_INVERSION_DISABLED_REQUEST = 4046,
        GET_PHASE_INVERSION_DISABLED_REQUEST = 4047
    }
}
