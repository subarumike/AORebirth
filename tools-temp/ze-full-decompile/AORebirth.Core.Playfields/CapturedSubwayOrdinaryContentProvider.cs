using System;
using System.Collections.Generic;
using System.Linq;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine.Core;

namespace AORebirth.Core.Playfields;

internal sealed class CapturedSubwayOrdinaryContentProvider
{
	public const int SubwayPlayfieldInstance = 127;

	private static readonly CapturedSubwayCorpseEvidenceDefinition[] SupportedCorpseEvidence = new CapturedSubwayCorpseEvidenceDefinition[134]
	{
		new CapturedSubwayCorpseEvidenceDefinition("20260708-004038", "2026-07-08T05:42:13.1283629Z", "(Corpse:00F6E00F)", "(SimpleChar:794ADB99)", 5, 26092, 5907, 29),
		new CapturedSubwayCorpseEvidenceDefinition("20260708-004038", "2026-07-08T05:42:28.7676408Z", "(Corpse:00F6E00F)", "(SimpleChar:794A16EE)", 10, 17720, 15929, 35),
		new CapturedSubwayCorpseEvidenceDefinition("20260708-004038", "2026-07-08T05:44:19.0256869Z", "(Corpse:00F6E007)", "(SimpleChar:794AD9A9)", 4, 17657, 15231, 23),
		new CapturedSubwayCorpseEvidenceDefinition("20260708-004038", "2026-07-08T05:45:12.2764697Z", "(Corpse:00F6E01E)", "(SimpleChar:794ADBC4)", 5, 17657, 15231, 29),
		new CapturedSubwayCorpseEvidenceDefinition("20260708-143600", "2026-07-08T19:36:15.0738631Z", "(Corpse:00F6E00E)", "(SimpleChar:794DF18C)", 5, 17657, 15231, 29),
		new CapturedSubwayCorpseEvidenceDefinition("20260708-143600", "2026-07-08T19:36:41.0935241Z", "(Corpse:00F6E003)", "(SimpleChar:794DF1A3)", 4, 17657, 15231, 23),
		new CapturedSubwayCorpseEvidenceDefinition("20260708-143600", "2026-07-08T19:36:47.0732957Z", "(Corpse:00F6E010)", "(SimpleChar:794DF15F)", 4, 17657, 15231, 23),
		new CapturedSubwayCorpseEvidenceDefinition("20260708-143600", "2026-07-08T19:37:07.0470664Z", "(Corpse:00F6E008)", "(SimpleChar:794DF169)", 4, 17657, 15231, 23),
		new CapturedSubwayCorpseEvidenceDefinition("20260708-143600", "2026-07-08T19:37:13.2974769Z", "(Corpse:00F6E012)", "(SimpleChar:794DF175)", 4, 17657, 15231, 23),
		new CapturedSubwayCorpseEvidenceDefinition("20260708-143600", "2026-07-08T19:38:01.9785407Z", "(Corpse:00F6E008)", "(SimpleChar:794DF15C)", 4, 17657, 15231, 23),
		new CapturedSubwayCorpseEvidenceDefinition("20260708-143600", "2026-07-08T19:38:22.9383494Z", "(Corpse:00F6E002)", "(SimpleChar:794DF17B)", 5, 17657, 15231, 29),
		new CapturedSubwayCorpseEvidenceDefinition("20260708-143600", "2026-07-08T19:38:58.8973001Z", "(Corpse:00F6E009)", "(SimpleChar:794DF1AF)", 8, 17649, 15215, 10),
		new CapturedSubwayCorpseEvidenceDefinition("20260708-143600", "2026-07-08T19:39:11.0377890Z", "(Corpse:00F6E006)", "(SimpleChar:794DF1D7)", 5, 203734, 17534, 44),
		new CapturedSubwayCorpseEvidenceDefinition("20260708-143600", "2026-07-08T19:39:19.9069001Z", "(Corpse:00F6E006)", "(SimpleChar:794DF195)", 5, 17657, 15231, 29),
		new CapturedSubwayCorpseEvidenceDefinition("20260708-143600", "2026-07-08T19:39:26.5669871Z", "(Corpse:00F6E006)", "(SimpleChar:794DF186)", 5, 17657, 15231, 29),
		new CapturedSubwayCorpseEvidenceDefinition("20260708-143600", "2026-07-08T19:39:44.4371390Z", "(Corpse:00F6E00C)", "(SimpleChar:794DF18A)", 6, 17657, 15231, 35),
		new CapturedSubwayCorpseEvidenceDefinition("20260708-143600", "2026-07-08T19:40:01.2673740Z", "(Corpse:00F6E004)", "(SimpleChar:794DF068)", 6, 203733, 17870, 21),
		new CapturedSubwayCorpseEvidenceDefinition("20260708-143600", "2026-07-08T19:40:25.5869914Z", "(Corpse:00F6E006)", "(SimpleChar:794DF1B0)", 8, 17657, 15231, 47),
		new CapturedSubwayCorpseEvidenceDefinition("20260708-143600", "2026-07-08T19:40:45.9573991Z", "(Corpse:00F6E007)", "(SimpleChar:794DF1A7)", 5, 17657, 15231, 29),
		new CapturedSubwayCorpseEvidenceDefinition("20260708-143600", "2026-07-08T19:41:12.0575936Z", "(Corpse:00F6E00C)", "(SimpleChar:794DF1B1)", 7, 17720, 15929, 25),
		new CapturedSubwayCorpseEvidenceDefinition("20260708-143600", "2026-07-08T19:42:06.3940173Z", "(Corpse:00F6E002)", "(SimpleChar:794DF1D4)", 7, 17720, 15929, 25),
		new CapturedSubwayCorpseEvidenceDefinition("20260708-143600", "2026-07-08T19:42:18.4150564Z", "(Corpse:00F6E002)", "(SimpleChar:794DF1BE)", 6, 17720, 15929, 21),
		new CapturedSubwayCorpseEvidenceDefinition("20260708-143600", "2026-07-08T19:42:44.5196497Z", "(Corpse:00F6E009)", "(SimpleChar:794ADC23)", 5, 17657, 15231, 29),
		new CapturedSubwayCorpseEvidenceDefinition("20260708-143600", "2026-07-08T19:43:46.8585900Z", "(Corpse:00F6E006)", "(SimpleChar:794DF1E0)", 7, 17720, 15929, 25),
		new CapturedSubwayCorpseEvidenceDefinition("20260708-143600", "2026-07-08T19:44:10.5938968Z", "(Corpse:00F6E018)", "(SimpleChar:794DF076)", 6, 203733, 17870, 21),
		new CapturedSubwayCorpseEvidenceDefinition("20260708-143600", "2026-07-08T19:44:44.7852039Z", "(Corpse:00F6E00D)", "(SimpleChar:794DF074)", 8, 17649, 15215, 10),
		new CapturedSubwayCorpseEvidenceDefinition("20260708-143600", "2026-07-08T19:45:11.8540394Z", "(Corpse:00F6E00E)", "(SimpleChar:794CD74B)", 7, 203733, 17870, 25),
		new CapturedSubwayCorpseEvidenceDefinition("20260708-143600", "2026-07-08T19:45:42.4960444Z", "(Corpse:00F6E001)", "(SimpleChar:794CD4CC)", 6, 203733, 17870, 21),
		new CapturedSubwayCorpseEvidenceDefinition("20260708-143600", "2026-07-08T19:46:05.6316305Z", "(Corpse:00F6E00A)", "(SimpleChar:794DF080)", 8, 203734, 17534, 71),
		new CapturedSubwayCorpseEvidenceDefinition("20260708-143600", "2026-07-08T19:46:30.8613088Z", "(Corpse:00F6E00A)", "(SimpleChar:794DF082)", 8, 203734, 17534, 71),
		new CapturedSubwayCorpseEvidenceDefinition("20260708-143600", "2026-07-08T19:46:37.9592025Z", "(Corpse:00F6E00A)", "(SimpleChar:794DF081)", 9, 203734, 17534, 80),
		new CapturedSubwayCorpseEvidenceDefinition("20260708-143600", "2026-07-08T19:47:02.9033847Z", "(Corpse:00F6E012)", "(SimpleChar:794DF083)", 8, 17720, 15929, 28),
		new CapturedSubwayCorpseEvidenceDefinition("20260708-143600", "2026-07-08T19:48:09.4041842Z", "(Corpse:00F6E015)", "(SimpleChar:794CD4D5)", 7, 17720, 15929, 25),
		new CapturedSubwayCorpseEvidenceDefinition("20260708-143600", "2026-07-08T19:48:43.0138606Z", "(Corpse:00F6E011)", "(SimpleChar:794ADBEF)", 9, 17720, 15929, 32),
		new CapturedSubwayCorpseEvidenceDefinition("20260708-143600", "2026-07-08T19:49:11.2829051Z", "(Corpse:00F6E002)", "(SimpleChar:794ADBF4)", 9, 17720, 15929, 32),
		new CapturedSubwayCorpseEvidenceDefinition("20260708-143600", "2026-07-08T19:53:32.1716326Z", "(Corpse:00F6E012)", "(SimpleChar:794CD769)", 9, 203734, 17534, 80),
		new CapturedSubwayCorpseEvidenceDefinition("20260708-143600", "2026-07-08T19:53:59.7613109Z", "(Corpse:00F6E004)", "(SimpleChar:794CD765)", 10, 203733, 17870, 35),
		new CapturedSubwayCorpseEvidenceDefinition("20260708-143600", "2026-07-08T19:54:28.4046893Z", "(Corpse:00F6E007)", "(SimpleChar:794CD760)", 10, 17720, 15929, 35),
		new CapturedSubwayCorpseEvidenceDefinition("20260708-143600", "2026-07-08T19:55:37.4650478Z", "(Corpse:00F6E005)", "(SimpleChar:794CD767)", 10, 17720, 15929, 35),
		new CapturedSubwayCorpseEvidenceDefinition("20260708-143600", "2026-07-08T19:56:20.7744362Z", "(Corpse:00F6E002)", "(SimpleChar:794CD7BA)", 10, 17720, 15929, 35),
		new CapturedSubwayCorpseEvidenceDefinition("20260708-143600", "2026-07-08T20:00:39.4770089Z", "(Corpse:00F6E006)", "(SimpleChar:794CD78B)", 10, 203734, 17534, 88),
		new CapturedSubwayCorpseEvidenceDefinition("20260708-143600", "2026-07-08T20:01:37.2671095Z", "(Corpse:00F6E016)", "(SimpleChar:794DF33F)", 10, 203733, 17870, 35),
		new CapturedSubwayCorpseEvidenceDefinition("20260708-143600", "2026-07-08T20:02:10.7306094Z", "(Corpse:00F6E005)", "(SimpleChar:794DF343)", 10, 17720, 15929, 35),
		new CapturedSubwayCorpseEvidenceDefinition("20260708-143600", "2026-07-08T20:03:55.2477489Z", "(Corpse:00F6E006)", "(SimpleChar:794DF34E)", 9, 17720, 15929, 32),
		new CapturedSubwayCorpseEvidenceDefinition("20260709-205921", "2026-07-10T02:03:30.1606625Z", "(Corpse:00F6E013)", "(SimpleChar:795310FB)", 8, 17649, 15215, 10),
		new CapturedSubwayCorpseEvidenceDefinition("20260709-205921", "2026-07-10T02:03:50.9754522Z", "(Corpse:00F6E012)", "(SimpleChar:7953178A)", 6, 17720, 15929, 21),
		new CapturedSubwayCorpseEvidenceDefinition("20260709-210452", "2026-07-10T02:05:16.2220694Z", "(Corpse:00F6E01E)", "(SimpleChar:79531789)", 7, 17720, 15929, 25),
		new CapturedSubwayCorpseEvidenceDefinition("20260709-210452", "2026-07-10T02:05:24.5890734Z", "(Corpse:00F6E01F)", "(SimpleChar:795317EF)", 5, 17720, 15929, 18),
		new CapturedSubwayCorpseEvidenceDefinition("20260709-210452", "2026-07-10T02:05:43.7375936Z", "(Corpse:00F6E012)", "(SimpleChar:7953ABA3)", 6, 203733, 17870, 21),
		new CapturedSubwayCorpseEvidenceDefinition("20260709-210452", "2026-07-10T02:05:58.6536106Z", "(Corpse:00F6E00F)", "(SimpleChar:794DF1E3)", 7, 17720, 15929, 25),
		new CapturedSubwayCorpseEvidenceDefinition("20260709-210452", "2026-07-10T02:06:17.6521103Z", "(Corpse:00F6E024)", "(SimpleChar:794DF1FE)", 5, 17657, 15231, 29),
		new CapturedSubwayCorpseEvidenceDefinition("20260709-210452", "2026-07-10T02:06:26.7185068Z", "(Corpse:00F6E025)", "(SimpleChar:794DF206)", 6, 17657, 15231, 35),
		new CapturedSubwayCorpseEvidenceDefinition("20260709-210452", "2026-07-10T02:06:57.1671915Z", "(Corpse:00F6E032)", "(SimpleChar:7947A4E2)", 5, 17657, 15231, 29),
		new CapturedSubwayCorpseEvidenceDefinition("20260709-210452", "2026-07-10T02:07:03.6002324Z", "(Corpse:00F6E030)", "(SimpleChar:7947A4ED)", 6, 17657, 15231, 35),
		new CapturedSubwayCorpseEvidenceDefinition("20260709-210452", "2026-07-10T02:07:12.1489306Z", "(Corpse:00F6E002)", "(SimpleChar:7947A4E3)", 7, 17657, 15231, 41),
		new CapturedSubwayCorpseEvidenceDefinition("20260709-210452", "2026-07-10T02:07:34.6995521Z", "(Corpse:00F6E030)", "(SimpleChar:794E807A)", 5, 17649, 15215, 6),
		new CapturedSubwayCorpseEvidenceDefinition("20260709-210452", "2026-07-10T02:07:43.9317723Z", "(Corpse:00F6E01D)", "(SimpleChar:794F60C6)", 5, 17649, 15215, 6),
		new CapturedSubwayCorpseEvidenceDefinition("20260709-210452", "2026-07-10T02:08:08.0979867Z", "(Corpse:00F6E02B)", "(SimpleChar:794F6080)", 6, 17649, 15215, 8),
		new CapturedSubwayCorpseEvidenceDefinition("20260709-210452", "2026-07-10T02:08:16.3979964Z", "(Corpse:00F6E02C)", "(SimpleChar:7953ABAB)", 6, 203733, 17870, 21),
		new CapturedSubwayCorpseEvidenceDefinition("20260709-210452", "2026-07-10T02:08:31.3973977Z", "(Corpse:00F6E020)", "(SimpleChar:79531279)", 6, 203733, 17870, 21),
		new CapturedSubwayCorpseEvidenceDefinition("20260709-210452", "2026-07-10T02:08:40.3142243Z", "(Corpse:00F6E02D)", "(SimpleChar:79528A5F)", 7, 203733, 17870, 25),
		new CapturedSubwayCorpseEvidenceDefinition("20260709-210452", "2026-07-10T02:09:03.6292212Z", "(Corpse:00F6E00F)", "(SimpleChar:7953AD55)", 8, 203734, 17534, 71),
		new CapturedSubwayCorpseEvidenceDefinition("20260709-210452", "2026-07-10T02:09:16.1953799Z", "(Corpse:00F6E01F)", "(SimpleChar:7953AD5C)", 8, 203734, 17534, 71),
		new CapturedSubwayCorpseEvidenceDefinition("20260709-210452", "2026-07-10T02:09:33.9454248Z", "(Corpse:00F6E011)", "(SimpleChar:7953AD53)", 9, 203734, 17534, 80),
		new CapturedSubwayCorpseEvidenceDefinition("20260709-210452", "2026-07-10T02:09:46.5103650Z", "(Corpse:00F6E014)", "(SimpleChar:794F626A)", 7, 17720, 15929, 25),
		new CapturedSubwayCorpseEvidenceDefinition("20260709-210452", "2026-07-10T02:13:01.6689423Z", "(Corpse:00F6E003)", "(SimpleChar:7953ADC6)", 9, 203734, 17534, 80),
		new CapturedSubwayCorpseEvidenceDefinition("20260709-210452", "2026-07-10T02:14:23.2322905Z", "(Corpse:00F6E017)", "(SimpleChar:79528F80)", 10, 203733, 17870, 35),
		new CapturedSubwayCorpseEvidenceDefinition("20260709-210452", "2026-07-10T02:15:06.3795943Z", "(Corpse:00F6E029)", "(SimpleChar:7953ADC8)", 10, 203734, 17534, 88),
		new CapturedSubwayCorpseEvidenceDefinition("20260709-212336", "2026-07-10T02:23:52.5889972Z", "(Corpse:00F6E015)", "(SimpleChar:7953AFF5)", 8, 203734, 17534, 71),
		new CapturedSubwayCorpseEvidenceDefinition("20260709-212336", "2026-07-10T02:23:56.7553699Z", "(Corpse:00F6E01B)", "(SimpleChar:7953AFFA)", 9, 203734, 17534, 80),
		new CapturedSubwayCorpseEvidenceDefinition("20260709-212336", "2026-07-10T02:24:14.4868133Z", "(Corpse:00F6E00E)", "(SimpleChar:7953AFEF)", 9, 203734, 17534, 80),
		new CapturedSubwayCorpseEvidenceDefinition("20260709-212336", "2026-07-10T02:24:56.9193162Z", "(Corpse:00F6E010)", "(SimpleChar:7953AF3C)", 5, 203734, 17534, 44),
		new CapturedSubwayCorpseEvidenceDefinition("20260709-212336", "2026-07-10T02:26:35.5983997Z", "(Corpse:00F6E002)", "(SimpleChar:7953AD49)", 6, 203733, 17870, 21),
		new CapturedSubwayCorpseEvidenceDefinition("20260709-212336", "2026-07-10T02:35:43.5583592Z", "(Corpse:00F6E00E)", "(SimpleChar:7953AA11)", 8, 203734, 17534, 71),
		new CapturedSubwayCorpseEvidenceDefinition("20260709-220439", "2026-07-10T03:06:20.7295058Z", "(Corpse:00F6E009)", "(SimpleChar:7953AD69)", 9, 17649, 15215, 11),
		new CapturedSubwayCorpseEvidenceDefinition("20260709-220439", "2026-07-10T03:06:38.1442368Z", "(Corpse:00F6E00A)", "(SimpleChar:7953AD6B)", 10, 203734, 17534, 88),
		new CapturedSubwayCorpseEvidenceDefinition("20260709-220439", "2026-07-10T03:07:04.4432411Z", "(Corpse:00F6E00B)", "(SimpleChar:7953AA81)", 10, 17649, 15215, 12),
		new CapturedSubwayCorpseEvidenceDefinition("20260709-220439", "2026-07-10T03:07:36.7245478Z", "(Corpse:00F6E014)", "(SimpleChar:7953A9E1)", 13, 17657, 15231, 79),
		new CapturedSubwayCorpseEvidenceDefinition("20260709-220439", "2026-07-10T03:07:40.2238405Z", "(Corpse:00F6E015)", "(SimpleChar:7953A9FC)", 11, 17657, 15231, 66),
		new CapturedSubwayCorpseEvidenceDefinition("20260709-220439", "2026-07-10T03:07:43.8739950Z", "(Corpse:00F6E016)", "(SimpleChar:7953A9E7)", 11, 17657, 15231, 66),
		new CapturedSubwayCorpseEvidenceDefinition("20260709-220439", "2026-07-10T03:07:54.5409875Z", "(Corpse:00F6E008)", "(SimpleChar:7953A9EA)", 11, 17657, 15231, 66),
		new CapturedSubwayCorpseEvidenceDefinition("20260709-220439", "2026-07-10T03:08:06.8221084Z", "(Corpse:00F6E00E)", "(SimpleChar:79513A8F)", 12, 17657, 15231, 72),
		new CapturedSubwayCorpseEvidenceDefinition("20260709-220439", "2026-07-10T03:08:10.5057903Z", "(Corpse:00F6E00F)", "(SimpleChar:79513A87)", 12, 17657, 15231, 72),
		new CapturedSubwayCorpseEvidenceDefinition("20260709-220439", "2026-07-10T03:08:14.0550241Z", "(Corpse:00F6E010)", "(SimpleChar:79513AC2)", 13, 17657, 15231, 79),
		new CapturedSubwayCorpseEvidenceDefinition("20260709-220439", "2026-07-10T03:08:19.3217153Z", "(Corpse:00F6E018)", "(SimpleChar:79513AAF)", 13, 17657, 15231, 79),
		new CapturedSubwayCorpseEvidenceDefinition("20260709-220439", "2026-07-10T03:08:45.1875311Z", "(Corpse:00F6E006)", "(SimpleChar:7953AA1B)", 10, 17720, 15929, 35),
		new CapturedSubwayCorpseEvidenceDefinition("20260709-220439", "2026-07-10T03:08:48.7873927Z", "(Corpse:00F6E01A)", "(SimpleChar:7953AA82)", 10, 17720, 15929, 35),
		new CapturedSubwayCorpseEvidenceDefinition("20260709-220439", "2026-07-10T03:08:52.4543010Z", "(Corpse:00F6E01C)", "(SimpleChar:7953AB08)", 10, 17649, 15215, 12),
		new CapturedSubwayCorpseEvidenceDefinition("20260709-220439", "2026-07-10T03:08:55.9372771Z", "(Corpse:00F6E01D)", "(SimpleChar:7953AA04)", 10, 17720, 15929, 35),
		new CapturedSubwayCorpseEvidenceDefinition("20260709-220439", "2026-07-10T03:09:31.7713590Z", "(Corpse:00F6E022)", "(SimpleChar:7953A880)", 13, 17657, 15231, 79),
		new CapturedSubwayCorpseEvidenceDefinition("20260709-220439", "2026-07-10T03:09:50.1025547Z", "(Corpse:00F6E009)", "(SimpleChar:7953A884)", 11, 17657, 15231, 66),
		new CapturedSubwayCorpseEvidenceDefinition("20260709-222339", "2026-07-10T03:25:04.6952789Z", "(Corpse:00F6E01B)", "(SimpleChar:795451FE)", 10, 203734, 17534, 88),
		new CapturedSubwayCorpseEvidenceDefinition("20260709-225408", "2026-07-10T03:54:36.7336153Z", "(Corpse:00F6E006)", "(SimpleChar:795452E5)", 5, 203734, 17534, 44),
		new CapturedSubwayCorpseEvidenceDefinition("20260709-225408", "2026-07-10T03:54:52.8156429Z", "(Corpse:00F6E007)", "(SimpleChar:79545306)", 6, 203733, 17870, 21),
		new CapturedSubwayCorpseEvidenceDefinition("20260709-225408", "2026-07-10T03:55:05.1981546Z", "(Corpse:00F6E00A)", "(SimpleChar:795317F5)", 7, 17657, 15231, 41),
		new CapturedSubwayCorpseEvidenceDefinition("20260709-225408", "2026-07-10T03:55:11.0652918Z", "(Corpse:00F6E00B)", "(SimpleChar:7953AD4A)", 7, 203733, 17870, 25),
		new CapturedSubwayCorpseEvidenceDefinition("20260709-225408", "2026-07-10T03:55:11.1825175Z", "(Corpse:00F6E00C)", "(SimpleChar:7953AD4C)", 7, 203733, 17870, 25),
		new CapturedSubwayCorpseEvidenceDefinition("20260709-225408", "2026-07-10T03:55:19.0981699Z", "(Corpse:00F6E015)", "(SimpleChar:795450FE)", 6, 203733, 17870, 21),
		new CapturedSubwayCorpseEvidenceDefinition("20260709-225408", "2026-07-10T03:55:40.4462779Z", "(Corpse:00F6E00D)", "(SimpleChar:79545329)", 10, 203734, 17534, 88),
		new CapturedSubwayCorpseEvidenceDefinition("20260709-225408", "2026-07-10T03:55:43.9969075Z", "(Corpse:00F6E016)", "(SimpleChar:7953AD70)", 11, 17657, 15231, 66),
		new CapturedSubwayCorpseEvidenceDefinition("20260709-225408", "2026-07-10T03:55:47.5638760Z", "(Corpse:00F6E018)", "(SimpleChar:7953AD71)", 11, 17657, 15231, 66),
		new CapturedSubwayCorpseEvidenceDefinition("20260709-225408", "2026-07-10T03:57:15.0929043Z", "(Corpse:00F6E013)", "(SimpleChar:7953AA0C)", 13, 17657, 15231, 79),
		new CapturedSubwayCorpseEvidenceDefinition("20260709-225408", "2026-07-10T03:57:18.6586242Z", "(Corpse:00F6E020)", "(SimpleChar:79545309)", 16, 17657, 15231, 98),
		new CapturedSubwayCorpseEvidenceDefinition("20260709-225408", "2026-07-10T03:57:25.9438458Z", "(Corpse:00F6E021)", "(SimpleChar:7953A9C2)", 15, 17657, 15231, 92),
		new CapturedSubwayCorpseEvidenceDefinition("20260709-225408", "2026-07-10T03:59:29.7880050Z", "(Corpse:00F6E00E)", "(SimpleChar:79545191)", 19, 17657, 15231, 118),
		new CapturedSubwayCorpseEvidenceDefinition("20260709-225408", "2026-07-10T04:00:57.9151945Z", "(Corpse:00F6E006)", "(SimpleChar:7953AF71)", 21, 17657, 15231, 131),
		new CapturedSubwayCorpseEvidenceDefinition("20260709-225408", "2026-07-10T04:01:04.8821069Z", "(Corpse:00F6E01C)", "(SimpleChar:7953AF6D)", 19, 17657, 15231, 118),
		new CapturedSubwayCorpseEvidenceDefinition("20260709-225408", "2026-07-10T04:01:11.7488896Z", "(Corpse:00F6E007)", "(SimpleChar:795451A4)", 21, 17657, 15231, 131),
		new CapturedSubwayCorpseEvidenceDefinition("20260709-225408", "2026-07-10T04:01:14.0148849Z", "(Corpse:00F6E01D)", "(SimpleChar:7953AF76)", 20, 17657, 15231, 124),
		new CapturedSubwayCorpseEvidenceDefinition("20260710-202132", "2026-07-11T01:25:12.5774101Z", "(Corpse:00F6C001)", "(SimpleChar:7957E5CA)", 10, 203734, 17534, 88),
		new CapturedSubwayCorpseEvidenceDefinition("20260712-153918", "2026-07-12T20:40:07.7995730Z", "(Corpse:00F6C002)", "(SimpleChar:795EC774)", 4, 17657, 15231, 23),
		new CapturedSubwayCorpseEvidenceDefinition("20260712-153918", "2026-07-12T20:40:31.8265708Z", "(Corpse:00F6C005)", "(SimpleChar:795EC775)", 6, 17657, 15231, 35),
		new CapturedSubwayCorpseEvidenceDefinition("20260712-153918", "2026-07-12T20:41:13.7488278Z", "(Corpse:00F6C002)", "(SimpleChar:795EC781)", 5, 203734, 17534, 44),
		new CapturedSubwayCorpseEvidenceDefinition("20260712-153918", "2026-07-12T20:42:19.8937755Z", "(Corpse:00F6C001)", "(SimpleChar:795EC786)", 7, 17720, 15929, 25),
		new CapturedSubwayCorpseEvidenceDefinition("20260712-153918", "2026-07-12T20:43:09.6887955Z", "(Corpse:00F6C007)", "(SimpleChar:795F910E)", 5, 26092, 5907, 29),
		new CapturedSubwayCorpseEvidenceDefinition("20260712-153918", "2026-07-12T20:43:26.2936812Z", "(Corpse:00F6C00B)", "(SimpleChar:795F9194)", 5, 17657, 15231, 29),
		new CapturedSubwayCorpseEvidenceDefinition("20260712-153918", "2026-07-12T20:43:28.8881673Z", "(Corpse:00F6C00D)", "(SimpleChar:795EC78D)", 6, 17649, 15215, 8),
		new CapturedSubwayCorpseEvidenceDefinition("20260712-153918", "2026-07-12T20:43:37.3920316Z", "(Corpse:00F6C00D)", "(SimpleChar:795F9195)", 4, 17657, 15231, 23),
		new CapturedSubwayCorpseEvidenceDefinition("20260712-153918", "2026-07-12T20:44:58.7438766Z", "(Corpse:00F6C01A)", "(SimpleChar:795EC7AE)", 6, 17720, 15929, 21),
		new CapturedSubwayCorpseEvidenceDefinition("20260712-153918", "2026-07-12T20:45:28.3441892Z", "(Corpse:00F6C01F)", "(SimpleChar:795EC0CD)", 7, 203733, 17870, 25),
		new CapturedSubwayCorpseEvidenceDefinition("20260712-153918", "2026-07-12T20:46:37.5983032Z", "(Corpse:00F6C007)", "(SimpleChar:795F91A4)", 5, 203734, 17534, 44),
		new CapturedSubwayCorpseEvidenceDefinition("20260712-155528", "2026-07-12T20:55:48.0982051Z", "(Corpse:00F6C003)", "(SimpleChar:795F91B9)", 5, 17657, 15231, 29),
		new CapturedSubwayCorpseEvidenceDefinition("20260712-160257", "2026-07-12T21:04:32.3274842Z", "(Corpse:00F6C006)", "(SimpleChar:795EC78A)", 9, 17649, 15215, 11),
		new CapturedSubwayCorpseEvidenceDefinition("20260712-161506", "2026-07-12T21:15:15.9221075Z", "(Corpse:00F6C00B)", "(SimpleChar:795F924E)", 4, 17657, 15231, 23),
		new CapturedSubwayCorpseEvidenceDefinition("20260712-161506", "2026-07-12T21:16:42.1576861Z", "(Corpse:00F6C012)", "(SimpleChar:795F91CA)", 5, 26092, 5907, 29),
		new CapturedSubwayCorpseEvidenceDefinition("20260713-013906", "2026-07-13T06:39:26.9937677Z", "(Corpse:00F6C004)", "(SimpleChar:79607CCB)", 5, 203734, 17534, 44),
		new CapturedSubwayCorpseEvidenceDefinition("20260713-013906", "2026-07-13T06:40:19.7027857Z", "(Corpse:00F6C001)", "(SimpleChar:79607B30)", 9, 17720, 15929, 32),
		new CapturedSubwayCorpseEvidenceDefinition("20260713-014714", "2026-07-13T06:47:41.0820075Z", "(Corpse:00F6C005)", "(SimpleChar:79607CD0)", 9, 17649, 15215, 11),
		new CapturedSubwayCorpseEvidenceDefinition("20260713-033511", "2026-07-13T08:36:05.9631613Z", "(Corpse:00F6C003)", "(SimpleChar:79607E2C)", 8, 17649, 15215, 10),
		new CapturedSubwayCorpseEvidenceDefinition("20260719-020104", "2026-07-19T07:02:15.8972817Z", "(Corpse:00F74001)", "(SimpleChar:797B885E)", 7, 203733, 17870, 25),
		new CapturedSubwayCorpseEvidenceDefinition("20260719-020104", "2026-07-19T07:02:56.9695786Z", "(Corpse:00F74002)", "(SimpleChar:797B885D)", 6, 203733, 17870, 21),
		new CapturedSubwayCorpseEvidenceDefinition("20260719-020104", "2026-07-19T07:03:00.1186314Z", "(Corpse:00F74003)", "(SimpleChar:797B885C)", 6, 203733, 17870, 21),
		new CapturedSubwayCorpseEvidenceDefinition("20260719-020104", "2026-07-19T07:03:03.9297870Z", "(Corpse:00F74004)", "(SimpleChar:797AD6E4)", 6, 17649, 15215, 8),
		new CapturedSubwayCorpseEvidenceDefinition("20260719-021022", "2026-07-19T07:13:38.0213742Z", "(Corpse:00F74005)", "(SimpleChar:797B889D)", 5, 203734, 17534, 44)
	};

	private static readonly CapturedSubwayLootOutcomeEvidenceDefinition[] SupportedLootOutcomeEvidence = new CapturedSubwayLootOutcomeEvidenceDefinition[91]
	{
		new CapturedSubwayLootOutcomeEvidenceDefinition("20260708-143600", "2026-07-08T19:39:14.1784184Z", "(Corpse:00F6E006)", "(SimpleChar:794DF1D7)", 203734, 3840, 0, 25822, 25831, 5),
		new CapturedSubwayLootOutcomeEvidenceDefinition("20260708-143600", "2026-07-08T19:39:14.1784184Z", "(Corpse:00F6E006)", "(SimpleChar:794DF1D7)", 203734, 3840, 1, 130060, 130061, 5),
		new CapturedSubwayLootOutcomeEvidenceDefinition("20260708-143600", "2026-07-08T19:40:07.3188579Z", "(Corpse:00F6E004)", "(SimpleChar:794DF068)", 203733, 5012, 0, 130586, 130586, 1),
		new CapturedSubwayLootOutcomeEvidenceDefinition("20260708-143600", "2026-07-08T19:40:07.3188579Z", "(Corpse:00F6E004)", "(SimpleChar:794DF068)", 203733, 5012, 1, 258543, 258543, 1),
		new CapturedSubwayLootOutcomeEvidenceDefinition("20260708-143600", "2026-07-08T19:42:26.7992501Z", "(Corpse:00F6E002)", "(SimpleChar:794DF1BE)", 17720, 7687, 0, 112798, 112799, 6),
		new CapturedSubwayLootOutcomeEvidenceDefinition("20260708-143600", "2026-07-08T19:42:28.8101423Z", "(Corpse:00F6E00C)", "(SimpleChar:794DF1B1)", 17720, 7728, 0, 234874, 234874, 1),
		new CapturedSubwayLootOutcomeEvidenceDefinition("20260708-143600", "2026-07-08T19:42:28.8101423Z", "(Corpse:00F6E00C)", "(SimpleChar:794DF1B1)", 17720, 7728, 1, 290619, 202727, 9),
		new CapturedSubwayLootOutcomeEvidenceDefinition("20260708-143600", "2026-07-08T19:43:52.6571195Z", "(Corpse:00F6E006)", "(SimpleChar:794DF1E0)", 17720, 9248, 0, 234876, 234876, 1),
		new CapturedSubwayLootOutcomeEvidenceDefinition("20260708-143600", "2026-07-08T19:44:14.8155132Z", "(Corpse:00F6E018)", "(SimpleChar:794DF076)", 203733, 9826, 0, 130586, 130586, 1),
		new CapturedSubwayLootOutcomeEvidenceDefinition("20260708-143600", "2026-07-08T19:44:14.8155132Z", "(Corpse:00F6E018)", "(SimpleChar:794DF076)", 203733, 9826, 1, 258543, 258543, 1),
		new CapturedSubwayLootOutcomeEvidenceDefinition("20260708-143600", "2026-07-08T19:44:14.8155132Z", "(Corpse:00F6E018)", "(SimpleChar:794DF076)", 203733, 9826, 2, 122140, 122141, 7),
		new CapturedSubwayLootOutcomeEvidenceDefinition("20260708-143600", "2026-07-08T19:45:36.5631878Z", "(Corpse:00F6E00E)", "(SimpleChar:794CD74B)", 203733, 11106, 0, 258543, 258543, 1),
		new CapturedSubwayLootOutcomeEvidenceDefinition("20260708-143600", "2026-07-08T19:45:45.9166367Z", "(Corpse:00F6E001)", "(SimpleChar:794CD4CC)", 203733, 11283, 0, 258543, 258543, 1),
		new CapturedSubwayLootOutcomeEvidenceDefinition("20260708-143600", "2026-07-08T19:45:45.9166367Z", "(Corpse:00F6E001)", "(SimpleChar:794CD4CC)", 203733, 11283, 1, 152326, 152327, 6),
		new CapturedSubwayLootOutcomeEvidenceDefinition("20260708-143600", "2026-07-08T19:46:13.6129919Z", "(Corpse:00F6E00A)", "(SimpleChar:794DF080)", 203734, 12113, 0, 136638, 136639, 9),
		new CapturedSubwayLootOutcomeEvidenceDefinition("20260708-143600", "2026-07-08T19:46:34.8815762Z", "(Corpse:00F6E00A)", "(SimpleChar:794DF082)", 203734, 12659, 0, 124348, 124349, 7),
		new CapturedSubwayLootOutcomeEvidenceDefinition("20260708-143600", "2026-07-08T19:48:12.3036894Z", "(Corpse:00F6E015)", "(SimpleChar:794CD4D5)", 17720, 13984, 0, 234874, 234874, 1),
		new CapturedSubwayLootOutcomeEvidenceDefinition("20260708-143600", "2026-07-08T19:48:12.3036894Z", "(Corpse:00F6E015)", "(SimpleChar:794CD4D5)", 17720, 13984, 1, 109520, 109521, 7),
		new CapturedSubwayLootOutcomeEvidenceDefinition("20260708-143600", "2026-07-08T19:49:12.9731708Z", "(Corpse:00F6E002)", "(SimpleChar:794ADBF4)", 17720, 14453, 0, 234874, 234874, 1),
		new CapturedSubwayLootOutcomeEvidenceDefinition("20260708-143600", "2026-07-08T19:49:12.9731708Z", "(Corpse:00F6E002)", "(SimpleChar:794ADBF4)", 17720, 14453, 1, 106005, 106006, 11),
		new CapturedSubwayLootOutcomeEvidenceDefinition("20260708-143600", "2026-07-08T19:53:36.2110591Z", "(Corpse:00F6E012)", "(SimpleChar:794CD769)", 203734, 16165, 0, 123976, 123977, 9),
		new CapturedSubwayLootOutcomeEvidenceDefinition("20260708-143600", "2026-07-08T19:54:31.2243252Z", "(Corpse:00F6E007)", "(SimpleChar:794CD760)", 17720, 16580, 0, 102283, 102284, 9),
		new CapturedSubwayLootOutcomeEvidenceDefinition("20260708-143600", "2026-07-08T19:55:41.8941333Z", "(Corpse:00F6E005)", "(SimpleChar:794CD767)", 17720, 17057, 0, 103973, 103974, 10),
		new CapturedSubwayLootOutcomeEvidenceDefinition("20260708-143600", "2026-07-08T19:56:22.3633325Z", "(Corpse:00F6E002)", "(SimpleChar:794CD7BA)", 17720, 17339, 0, 234877, 234877, 1),
		new CapturedSubwayLootOutcomeEvidenceDefinition("20260708-143600", "2026-07-08T19:56:22.3633325Z", "(Corpse:00F6E002)", "(SimpleChar:794CD7BA)", 17720, 17339, 1, 107283, 107284, 10),
		new CapturedSubwayLootOutcomeEvidenceDefinition("20260708-143600", "2026-07-08T20:00:42.7180023Z", "(Corpse:00F6E006)", "(SimpleChar:794CD78B)", 203734, 19122, 0, 124545, 124546, 10),
		new CapturedSubwayLootOutcomeEvidenceDefinition("20260708-143600", "2026-07-08T20:01:40.3714720Z", "(Corpse:00F6E016)", "(SimpleChar:794DF33F)", 203733, 19546, 0, 130592, 130592, 1),
		new CapturedSubwayLootOutcomeEvidenceDefinition("20260708-143600", "2026-07-08T20:01:40.3714720Z", "(Corpse:00F6E016)", "(SimpleChar:794DF33F)", 203733, 19546, 1, 123704, 123705, 12),
		new CapturedSubwayLootOutcomeEvidenceDefinition("20260708-143600", "2026-07-08T20:02:28.9156431Z", "(Corpse:00F6E005)", "(SimpleChar:794DF343)", 17720, 19882, 0, 234876, 234876, 1),
		new CapturedSubwayLootOutcomeEvidenceDefinition("20260708-143600", "2026-07-08T20:03:59.9973733Z", "(Corpse:00F6E006)", "(SimpleChar:794DF34E)", 17720, 20529, 0, 101681, 101682, 7),
		new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-205921", "2026-07-10T01:59:53.471624Z", "(Corpse:00F6E026)", "(SimpleChar:7953AB9A)", 26092, 468, 0, 297055, 297055, 1),
		new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-205921", "2026-07-10T02:00:33.415859Z", "(Corpse:00F6E015)", "(SimpleChar:7953AD79)", 17657, 1020, 0, 234876, 234876, 1),
		new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-205921", "2026-07-10T02:00:33.415859Z", "(Corpse:00F6E015)", "(SimpleChar:7953AD79)", 17657, 1020, 1, 111623, 111624, 4),
		new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-205921", "2026-07-10T02:00:47.144983Z", "(Corpse:00F6E007)", "(SimpleChar:79531763)", 17657, 1271, 0, 104282, 104283, 3),
		new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-205921", "2026-07-10T02:02:04.295186Z", "(Corpse:00F6E00C)", "(SimpleChar:79531741)", 17657, 2461, 0, 102540, 102541, 4),
		new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-205921", "2026-07-10T02:02:18.645438Z", "(Corpse:00F6E022)", "(SimpleChar:7953175E)", 17657, 2751, 0, 234876, 234876, 1),
		new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-205921", "2026-07-10T02:02:18.645438Z", "(Corpse:00F6E022)", "(SimpleChar:7953175E)", 17657, 2751, 1, 109230, 109231, 5),
		new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-210452", "2026-07-10T02:05:22.371904Z", "(Corpse:00F6E01E)", "(SimpleChar:79531789)", 17720, 567, 0, 111623, 111624, 8),
		new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-210452", "2026-07-10T02:05:27.622774Z", "(Corpse:00F6E01F)", "(SimpleChar:795317EF)", 17720, 696, 0, 112160, 112161, 6),
		new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-210452", "2026-07-10T02:05:49.354265Z", "(Corpse:00F6E012)", "(SimpleChar:7953ABA3)", 203733, 1143, 0, 234876, 234876, 1),
		new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-210452", "2026-07-10T02:06:30.653733Z", "(Corpse:00F6E024)", "(SimpleChar:794DF1FE)", 17657, 2086, 0, 234874, 234874, 1),
		new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-210452", "2026-07-10T02:06:30.653733Z", "(Corpse:00F6E024)", "(SimpleChar:794DF1FE)", 17657, 2086, 1, 103110, 103111, 6),
		new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-210452", "2026-07-10T02:07:14.368995Z", "(Corpse:00F6E002)", "(SimpleChar:7947A4E3)", 17657, 2915, 0, 101581, 101582, 6),
		new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-210452", "2026-07-10T02:07:15.200998Z", "(Corpse:00F6E032)", "(SimpleChar:7947A4E2)", 17657, 2927, 0, 110874, 110875, 6),
		new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-210452", "2026-07-10T02:07:20.967056Z", "(Corpse:00F6E030)", "(SimpleChar:7947A4ED)", 17657, 3009, 0, 101507, 101508, 6),
		new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-210452", "2026-07-10T02:07:41.448887Z", "(Corpse:00F6E030)", "(SimpleChar:794E807A)", 17649, 3479, 0, 234877, 234877, 1),
		new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-210452", "2026-07-10T02:08:19.265416Z", "(Corpse:00F6E02C)", "(SimpleChar:7953ABAB)", 203733, 4276, 0, 130586, 130586, 1),
		new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-210452", "2026-07-10T02:08:19.265416Z", "(Corpse:00F6E02C)", "(SimpleChar:7953ABAB)", 203733, 4276, 1, 258543, 258543, 1),
		new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-210452", "2026-07-10T02:08:36.047549Z", "(Corpse:00F6E020)", "(SimpleChar:79531279)", 203733, 4597, 0, 130621, 130621, 1),
		new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-210452", "2026-07-10T02:09:06.711777Z", "(Corpse:00F6E00F)", "(SimpleChar:7953AD55)", 203734, 5230, 0, 136646, 136647, 9),
		new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-210452", "2026-07-10T02:09:06.711777Z", "(Corpse:00F6E00F)", "(SimpleChar:7953AD55)", 203734, 5230, 1, 128636, 128637, 8),
		new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-210452", "2026-07-10T02:09:19.546165Z", "(Corpse:00F6E01F)", "(SimpleChar:7953AD5C)", 203734, 5471, 0, 131605, 131606, 7),
		new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-210452", "2026-07-10T02:09:48.476009Z", "(Corpse:00F6E014)", "(SimpleChar:794F626A)", 17720, 5938, 0, 234876, 234876, 1),
		new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-210452", "2026-07-10T02:11:23.490766Z", "(Corpse:00F6E01D)", "(SimpleChar:794F60C6)", 17649, 6717, 0, 124465, 124466, 10),
		new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-210452", "2026-07-10T02:13:03.786201Z", "(Corpse:00F6E003)", "(SimpleChar:7953ADC6)", 203734, 7474, 0, 85711, 22014, 8),
		new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-210452", "2026-07-10T02:14:28.550195Z", "(Corpse:00F6E017)", "(SimpleChar:79528F80)", 203733, 8155, 0, 130592, 130592, 1),
		new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-210452", "2026-07-10T02:14:28.550195Z", "(Corpse:00F6E017)", "(SimpleChar:79528F80)", 203733, 8155, 1, 258543, 258543, 1),
		new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-210452", "2026-07-10T02:14:28.550195Z", "(Corpse:00F6E017)", "(SimpleChar:79528F80)", 203733, 8155, 2, 273381, 204397, 8),
		new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-210452", "2026-07-10T02:14:28.550195Z", "(Corpse:00F6E017)", "(SimpleChar:79528F80)", 203733, 8155, 3, 85531, 22289, 8),
		new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-210452", "2026-07-10T02:15:09.880347Z", "(Corpse:00F6E029)", "(SimpleChar:7953ADC8)", 203734, 8477, 0, 234876, 234876, 1),
		new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-210452", "2026-07-10T02:15:09.880347Z", "(Corpse:00F6E029)", "(SimpleChar:7953ADC8)", 203734, 8477, 1, 136638, 136639, 12),
		new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-212336", "2026-07-10T02:24:17.089024Z", "(Corpse:00F6E00E)", "(SimpleChar:7953AFEF)", 203734, 738, 0, 136640, 136641, 7),
		new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-212336", "2026-07-10T02:24:17.089024Z", "(Corpse:00F6E00E)", "(SimpleChar:7953AFEF)", 203734, 738, 1, 160224, 160225, 10),
		new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-212336", "2026-07-10T02:24:19.454512Z", "(Corpse:00F6E015)", "(SimpleChar:7953AFF5)", 203734, 777, 0, 234875, 234875, 1),
		new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-212336", "2026-07-10T02:24:19.454512Z", "(Corpse:00F6E015)", "(SimpleChar:7953AFF5)", 203734, 777, 1, 136640, 136641, 9),
		new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-212336", "2026-07-10T02:24:19.454512Z", "(Corpse:00F6E015)", "(SimpleChar:7953AFF5)", 203734, 777, 2, 130060, 130061, 9),
		new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-212336", "2026-07-10T02:25:00.386223Z", "(Corpse:00F6E010)", "(SimpleChar:7953AF3C)", 203734, 1740, 0, 123723, 123724, 6),
		new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-212336", "2026-07-10T02:35:45.440163Z", "(Corpse:00F6E00E)", "(SimpleChar:7953AA11)", 203734, 13668, 0, 123704, 123705, 9),
		new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-220439", "2026-07-10T03:08:18.173683Z", "(Corpse:00F6E00F)", "(SimpleChar:79513A87)", 17657, 7788, 0, 234876, 234876, 1),
		new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-220439", "2026-07-10T03:08:18.173683Z", "(Corpse:00F6E00F)", "(SimpleChar:79513A87)", 17657, 7788, 1, 101761, 101762, 9),
		new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-220439", "2026-07-10T03:08:19.473714Z", "(Corpse:00F6E010)", "(SimpleChar:79513AC2)", 17657, 7814, 0, 110192, 110193, 15),
		new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-225408", "2026-07-10T03:54:56.968200Z", "(Corpse:00F6E007)", "(SimpleChar:79545306)", 203733, 1496, 0, 130586, 130586, 1),
		new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-225408", "2026-07-10T03:54:56.968200Z", "(Corpse:00F6E007)", "(SimpleChar:79545306)", 203733, 1496, 1, 258543, 258543, 1),
		new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-225408", "2026-07-10T03:54:56.968200Z", "(Corpse:00F6E007)", "(SimpleChar:79545306)", 203733, 1496, 2, 128715, 128716, 6),
		new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-225408", "2026-07-10T04:01:17.043513Z", "(Corpse:00F6E01D)", "(SimpleChar:7953AF76)", 17657, 10799, 0, 112526, 112527, 16),
		new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-225408", "2026-07-10T04:01:17.616021Z", "(Corpse:00F6E007)", "(SimpleChar:795451A4)", 17657, 10814, 0, 107544, 107545, 20),
		new CapturedSubwayLootOutcomeEvidenceDefinition("20260710-202132", "2026-07-11T01:25:15.801207Z", "(Corpse:00F6C001)", "(SimpleChar:7957E5CA)", 203734, 4091, 0, 234875, 234875, 1),
		new CapturedSubwayLootOutcomeEvidenceDefinition("20260710-202132", "2026-07-11T01:25:15.801207Z", "(Corpse:00F6C001)", "(SimpleChar:7957E5CA)", 203734, 4091, 1, 136640, 136641, 8),
		new CapturedSubwayLootOutcomeEvidenceDefinition("20260710-202132", "2026-07-11T01:25:15.801207Z", "(Corpse:00F6C001)", "(SimpleChar:7957E5CA)", 203734, 4091, 2, 128839, 128840, 9),
		new CapturedSubwayLootOutcomeEvidenceDefinition("20260719-020104", "2026-07-19T07:02:21.808938Z", "(Corpse:00F74001)", "(SimpleChar:797B885E)", 203733, 1385, 0, 130586, 130586, 1),
		new CapturedSubwayLootOutcomeEvidenceDefinition("20260719-020104", "2026-07-19T07:02:21.808938Z", "(Corpse:00F74001)", "(SimpleChar:797B885E)", 203733, 1385, 1, 273381, 204397, 9),
		new CapturedSubwayLootOutcomeEvidenceDefinition("20260719-020104", "2026-07-19T07:03:04.379296Z", "(Corpse:00F74002)", "(SimpleChar:797B885D)", 203733, 2323, 0, 234877, 234877, 1),
		new CapturedSubwayLootOutcomeEvidenceDefinition("20260719-020104", "2026-07-19T07:03:04.379296Z", "(Corpse:00F74002)", "(SimpleChar:797B885D)", 203733, 2323, 1, 130586, 130586, 1),
		new CapturedSubwayLootOutcomeEvidenceDefinition("20260719-020104", "2026-07-19T07:03:04.379296Z", "(Corpse:00F74002)", "(SimpleChar:797B885D)", 203733, 2323, 2, 258543, 258543, 1),
		new CapturedSubwayLootOutcomeEvidenceDefinition("20260719-020104", "2026-07-19T07:03:04.379296Z", "(Corpse:00F74002)", "(SimpleChar:797B885D)", 203733, 2323, 3, 273381, 204397, 6),
		new CapturedSubwayLootOutcomeEvidenceDefinition("20260719-020104", "2026-07-19T07:03:04.379296Z", "(Corpse:00F74002)", "(SimpleChar:797B885D)", 203733, 2323, 4, 124545, 124546, 6),
		new CapturedSubwayLootOutcomeEvidenceDefinition("20260719-020104", "2026-07-19T07:03:04.999505Z", "(Corpse:00F74003)", "(SimpleChar:797B885C)", 203733, 2339, 0, 130607, 130607, 1),
		new CapturedSubwayLootOutcomeEvidenceDefinition("20260719-020104", "2026-07-19T07:03:04.999505Z", "(Corpse:00F74003)", "(SimpleChar:797B885C)", 203733, 2339, 1, 273381, 204397, 5),
		new CapturedSubwayLootOutcomeEvidenceDefinition("20260719-020104", "2026-07-19T07:03:04.999505Z", "(Corpse:00F74003)", "(SimpleChar:797B885C)", 203733, 2339, 2, 124016, 124017, 6),
		new CapturedSubwayLootOutcomeEvidenceDefinition("20260719-020104", "2026-07-19T07:03:07.113175Z", "(Corpse:00F74004)", "(SimpleChar:797AD6E4)", 17649, 2383, 0, 113398, 113399, 7),
		new CapturedSubwayLootOutcomeEvidenceDefinition("20260719-021022", "2026-07-19T07:13:41.373431Z", "(Corpse:00F74005)", "(SimpleChar:797B889D)", 203734, 3848, 0, 123495, 123496, 5)
	};

	private static readonly CapturedSubwaySourceWeaponProfileDefinition[] SupportedSourceWeaponProfiles = new CapturedSubwaySourceWeaponProfileDefinition[1]
	{
		new CapturedSubwaySourceWeaponProfileDefinition("Mugger", 203734, new CapturedSubwaySourceWeaponEvidenceDefinition[9]
		{
			new CapturedSubwaySourceWeaponEvidenceDefinition(2035526161, 121567, 121567, 1, "20260709-212115,20260709-212336"),
			new CapturedSubwaySourceWeaponEvidenceDefinition(2035527019, 121567, 121567, 1, "20260709-212115,20260709-212336,20260709-220439"),
			new CapturedSubwaySourceWeaponEvidenceDefinition(2035568852, 121567, 121567, 1, "20260709-212115,20260709-212336,20260709-220439"),
			new CapturedSubwaySourceWeaponEvidenceDefinition(2035569150, 121567, 121567, 1, "20260709-222339"),
			new CapturedSubwaySourceWeaponEvidenceDefinition(2035646228, 121567, 121567, 1, "20260710-202132"),
			new CapturedSubwaySourceWeaponEvidenceDefinition(2035803590, 121567, 121567, 1, "20260710-202132"),
			new CapturedSubwaySourceWeaponEvidenceDefinition(2035803591, 121567, 121567, 1, "20260710-202132,20260710-211430"),
			new CapturedSubwaySourceWeaponEvidenceDefinition(2035803592, 121567, 121567, 1, "20260710-202132"),
			new CapturedSubwaySourceWeaponEvidenceDefinition(2035803594, 121567, 121567, 1, "20260710-202132")
		})
	};

	private static readonly CapturedSubwayGenerationVariantDefinition[] GenerationVariants = new CapturedSubwayGenerationVariantDefinition[54]
	{
		new CapturedSubwayGenerationVariantDefinition(203728, 2035569008, 17, 368, 0, 98, 59, 122653, 122654, 18, "20260709-222339:(SimpleChar:79545170)"),
		new CapturedSubwayGenerationVariantDefinition(203728, 2035569008, 18, 394, 0, 98, 62, 122653, 122654, 16, "20260716-034559:(SimpleChar:796D4020)"),
		new CapturedSubwayGenerationVariantDefinition(203728, 2035569010, 18, 394, 0, 98, 62, 122653, 122654, 14, "20260709-222339:(SimpleChar:79545172)"),
		new CapturedSubwayGenerationVariantDefinition(203728, 2035569010, 18, 394, 0, 98, 62, 122653, 122654, 15, "20260716-034559:(SimpleChar:796D401E)"),
		new CapturedSubwayGenerationVariantDefinition(203728, 2035569015, 19, 421, 0, 98, 66, 122653, 122654, 18, "20260709-222339:(SimpleChar:79545177);20260716-034559:(SimpleChar:796D4010)"),
		new CapturedSubwayGenerationVariantDefinition(203728, 2035569015, 19, 421, 0, 98, 66, 122655, 122656, 22, "20260716-222007:(SimpleChar:79702459)"),
		new CapturedSubwayGenerationVariantDefinition(203728, 2035569025, 18, 394, 0, 98, 62, 122653, 122654, 15, "20260716-222007:(SimpleChar:79702463)"),
		new CapturedSubwayGenerationVariantDefinition(203728, 2035569025, 18, 394, 0, 98, 62, 122653, 122654, 16, "20260716-034559:(SimpleChar:796D4017)"),
		new CapturedSubwayGenerationVariantDefinition(203728, 2035569025, 19, 421, 0, 98, 66, 122653, 122654, 18, "20260717-215250:(SimpleChar:79748620)"),
		new CapturedSubwayGenerationVariantDefinition(203728, 2035569025, 19, 421, 0, 98, 66, 122654, 122654, 20, "20260709-222339:(SimpleChar:79545181)"),
		new CapturedSubwayGenerationVariantDefinition(203728, 2035569032, 19, 421, 0, 98, 66, 122653, 122654, 17, "20260709-222339:(SimpleChar:79545188)"),
		new CapturedSubwayGenerationVariantDefinition(203728, 2035569032, 19, 421, 0, 98, 66, 122655, 122655, 21, "20260716-034656:(SimpleChar:796D4003)"),
		new CapturedSubwayGenerationVariantDefinition(203728, 2035569032, 19, 421, 0, 98, 66, 122655, 122656, 23, "20260717-215250:(SimpleChar:79748630)"),
		new CapturedSubwayGenerationVariantDefinition(203728, 2035569084, 21, 474, 0, 99, 73, 122653, 122654, 18, "20260709-222339:(SimpleChar:795451BC)"),
		new CapturedSubwayGenerationVariantDefinition(203728, 2035569089, 19, 421, 0, 98, 66, 122655, 122655, 21, "20260709-222339:(SimpleChar:795451C1)"),
		new CapturedSubwayGenerationVariantDefinition(203728, 2035569099, 19, 421, 0, 98, 66, 122655, 122655, 21, "20260716-034559:(SimpleChar:796CD7DA)"),
		new CapturedSubwayGenerationVariantDefinition(203728, 2035569099, 20, 447, 0, 99, 69, 122655, 122656, 23, "20260716-222007:(SimpleChar:797024C6)"),
		new CapturedSubwayGenerationVariantDefinition(203728, 2035569099, 21, 474, 0, 99, 73, 122655, 122656, 24, "20260709-222339:(SimpleChar:795451CB)"),
		new CapturedSubwayGenerationVariantDefinition(203728, 2035569149, 19, 421, 0, 98, 66, 122654, 122654, 20, "20260709-222339:(SimpleChar:795451FD)"),
		new CapturedSubwayGenerationVariantDefinition(203728, 2035569149, 21, 474, 0, 99, 73, 122654, 122654, 20, "20260716-222007:(SimpleChar:7970254C)"),
		new CapturedSubwayGenerationVariantDefinition(203728, 2035569149, 22, 500, 0, 99, 76, 122655, 122656, 22, "20260716-033326:(SimpleChar:796D403C)"),
		new CapturedSubwayGenerationVariantDefinition(203728, 2035569217, 17, 368, 0, 98, 59, 122654, 122654, 20, "20260709-222339:(SimpleChar:79545241)"),
		new CapturedSubwayGenerationVariantDefinition(203728, 2035569217, 19, 421, 0, 98, 66, 122655, 122656, 22, "20260709-225408:(SimpleChar:79545352)"),
		new CapturedSubwayGenerationVariantDefinition(204178, 2035527557, 20, 782, 0, 99, 69, 122027, 122027, 20, "20260709-222339:(SimpleChar:7953AF85)"),
		new CapturedSubwayGenerationVariantDefinition(204178, 2035527557, 21, 829, 0, 99, 73, 122026, 122027, 19, "20260717-214612:(SimpleChar:7973F090)"),
		new CapturedSubwayGenerationVariantDefinition(204178, 2035527557, 21, 829, 0, 99, 73, 122028, 122029, 22, "20260716-034559:(SimpleChar:796CD6EF)"),
		new CapturedSubwayGenerationVariantDefinition(204178, 2035527557, 21, 829, 0, 99, 73, 122028, 122029, 25, "20260716-222007:(SimpleChar:79702286)"),
		new CapturedSubwayGenerationVariantDefinition(204178, 2035569087, 19, 736, 0, 98, 66, 122026, 122027, 14, "20260709-222339:(SimpleChar:795451BF)"),
		new CapturedSubwayGenerationVariantDefinition(204178, 2035569092, 20, 782, 0, 99, 69, 122026, 122027, 16, "20260716-034559:(SimpleChar:796CD7D0)"),
		new CapturedSubwayGenerationVariantDefinition(204178, 2035569092, 20, 782, 0, 99, 69, 122028, 122029, 23, "20260716-222007:(SimpleChar:797024B6)"),
		new CapturedSubwayGenerationVariantDefinition(204178, 2035569092, 21, 829, 0, 99, 73, 122028, 122029, 25, "20260709-222339:(SimpleChar:795451C4)"),
		new CapturedSubwayGenerationVariantDefinition(204178, 2035569107, 19, 736, 0, 98, 66, 122026, 122027, 16, "20260709-222339:(SimpleChar:795451D3)"),
		new CapturedSubwayGenerationVariantDefinition(204178, 2035569107, 22, 875, 0, 99, 76, 122026, 122027, 19, "20260716-220400:(SimpleChar:7970250F)"),
		new CapturedSubwayGenerationVariantDefinition(203729, 2035569002, 17, 368, 0, 98, 59, 123685, 123686, 14, "20260709-222339:(SimpleChar:7954516A);20260716-033326:(SimpleChar:796D403E)"),
		new CapturedSubwayGenerationVariantDefinition(203729, 2035569007, 17, 368, 0, 98, 59, 123685, 123686, 17, "20260709-222339:(SimpleChar:7954516F)"),
		new CapturedSubwayGenerationVariantDefinition(203729, 2035569007, 18, 394, 0, 98, 62, 123685, 123686, 18, "20260716-034559:(SimpleChar:796D401F)"),
		new CapturedSubwayGenerationVariantDefinition(203729, 2035569018, 18, 394, 0, 98, 62, 123686, 123686, 20, "20260716-034559:(SimpleChar:796D4013)"),
		new CapturedSubwayGenerationVariantDefinition(203729, 2035569018, 19, 421, 0, 98, 66, 123685, 123686, 19, "20260709-222339:(SimpleChar:7954517A)"),
		new CapturedSubwayGenerationVariantDefinition(203729, 2035569034, 18, 394, 0, 98, 62, 123685, 123686, 15, "20260716-034656:(SimpleChar:796D4002)"),
		new CapturedSubwayGenerationVariantDefinition(203729, 2035569034, 20, 447, 0, 99, 69, 123687, 123688, 25, "20260709-222339:(SimpleChar:7954518A)"),
		new CapturedSubwayGenerationVariantDefinition(203729, 2035569034, 21, 474, 0, 99, 73, 123685, 123686, 17, "20260717-215250:(SimpleChar:79748629)"),
		new CapturedSubwayGenerationVariantDefinition(203729, 2035569035, 18, 394, 0, 98, 62, 123685, 123686, 14, "20260709-222339:(SimpleChar:7954518B)"),
		new CapturedSubwayGenerationVariantDefinition(203729, 2035569035, 19, 421, 0, 98, 66, 123687, 123688, 23, "20260716-034656:(SimpleChar:796D4004)"),
		new CapturedSubwayGenerationVariantDefinition(203729, 2035569035, 20, 447, 0, 99, 69, 123685, 123686, 18, "20260717-215250:(SimpleChar:7974862E)"),
		new CapturedSubwayGenerationVariantDefinition(203729, 2035569038, 18, 394, 0, 98, 62, 123685, 123686, 17, "20260709-222339:(SimpleChar:7954518E)"),
		new CapturedSubwayGenerationVariantDefinition(203729, 2035569038, 18, 394, 0, 98, 62, 123686, 123686, 20, "20260717-215250:(SimpleChar:7974862B)"),
		new CapturedSubwayGenerationVariantDefinition(203729, 2035569066, 21, 474, 0, 99, 73, 123687, 123688, 26, "20260709-222339:(SimpleChar:795451AA)"),
		new CapturedSubwayGenerationVariantDefinition(203729, 2035569070, 21, 474, 0, 99, 73, 123687, 123688, 25, "20260709-222339:(SimpleChar:795451AE)"),
		new CapturedSubwayGenerationVariantDefinition(203729, 2035569224, 18, 394, 0, 98, 62, 123685, 123686, 18, "20260709-222339:(SimpleChar:79545248)"),
		new CapturedSubwayGenerationVariantDefinition(203729, 2035569224, 18, 394, 0, 98, 62, 123687, 123687, 21, "20260710-211430:(SimpleChar:7957E5F7)"),
		new CapturedSubwayGenerationVariantDefinition(203729, 2035569511, 18, 394, 0, 98, 62, 123685, 123686, 18, "20260716-033326:(SimpleChar:796D403F)"),
		new CapturedSubwayGenerationVariantDefinition(203729, 2035569511, 18, 394, 0, 98, 62, 123685, 123686, 19, "20260709-225408:(SimpleChar:79545367)"),
		new CapturedSubwayGenerationVariantDefinition(203727, 2035569494, 17, 368, 0, 98, 65, 0, 0, 0, "20260712-232848:(SimpleChar:79607A3B)"),
		new CapturedSubwayGenerationVariantDefinition(203727, 2035569494, 18, 394, 0, 98, 68, 0, 0, 0, "20260709-225408:(SimpleChar:79545356)")
	};

	private static readonly CapturedSubwayStrictLootProfileDefinition[] StrictLootProfiles = new CapturedSubwayStrictLootProfileDefinition[19]
	{
		new CapturedSubwayStrictLootProfileDefinition("Discarded Pet", 17720, 16, 13, 3, itemPoolComplete: false, new string[2] { "20260708-143600", "20260709-210452" }, new CapturedSubwayLootEvidenceDefinition[13]
		{
			new CapturedSubwayLootEvidenceDefinition(101681, 101682, 7, 1, 16, 625),
			new CapturedSubwayLootEvidenceDefinition(102283, 102284, 9, 1, 16, 625),
			new CapturedSubwayLootEvidenceDefinition(103973, 103974, 10, 1, 16, 625),
			new CapturedSubwayLootEvidenceDefinition(106005, 106006, 11, 1, 16, 625),
			new CapturedSubwayLootEvidenceDefinition(107283, 107284, 10, 1, 16, 625),
			new CapturedSubwayLootEvidenceDefinition(109520, 109521, 7, 1, 16, 625),
			new CapturedSubwayLootEvidenceDefinition(111623, 111624, 8, 1, 16, 625),
			new CapturedSubwayLootEvidenceDefinition(112160, 112161, 6, 1, 16, 625),
			new CapturedSubwayLootEvidenceDefinition(112798, 112799, 6, 1, 16, 625),
			new CapturedSubwayLootEvidenceDefinition(234874, 234874, 1, 3, 16, 1875),
			new CapturedSubwayLootEvidenceDefinition(234876, 234876, 1, 3, 16, 1875),
			new CapturedSubwayLootEvidenceDefinition(234877, 234877, 1, 1, 16, 625),
			new CapturedSubwayLootEvidenceDefinition(290619, 202727, 9, 1, 16, 625)
		}),
		new CapturedSubwayStrictLootProfileDefinition("Bloodcreeper", 30379, 4, 1, 3, itemPoolComplete: false, new string[4] { "20260716-033326", "20260716-034104", "20260716-221358", "20260717-214751" }, new CapturedSubwayLootEvidenceDefinition[1]
		{
			new CapturedSubwayLootEvidenceDefinition(42640, 42641, 30, 1, 4, 2500)
		}),
		new CapturedSubwayStrictLootProfileDefinition("Shadow", 30464, 15, 8, 7, itemPoolComplete: false, new string[2] { "20260709-212336", "20260712-223719" }, new CapturedSubwayLootEvidenceDefinition[10]
		{
			new CapturedSubwayLootEvidenceDefinition(21601, 21601, 1, 1, 15, 667),
			new CapturedSubwayLootEvidenceDefinition(27199, 27199, 10, 1, 15, 667),
			new CapturedSubwayLootEvidenceDefinition(121931, 121932, 15, 1, 15, 667),
			new CapturedSubwayLootEvidenceDefinition(122007, 122008, 12, 1, 15, 667),
			new CapturedSubwayLootEvidenceDefinition(123666, 123667, 9, 1, 15, 667),
			new CapturedSubwayLootEvidenceDefinition(124364, 124365, 10, 1, 15, 667),
			new CapturedSubwayLootEvidenceDefinition(124512, 124513, 28, 1, 15, 667),
			new CapturedSubwayLootEvidenceDefinition(152279, 152280, 18, 1, 15, 667),
			new CapturedSubwayLootEvidenceDefinition(234875, 234875, 1, 2, 15, 1333),
			new CapturedSubwayLootEvidenceDefinition(234876, 234876, 1, 1, 15, 667)
		}),
		new CapturedSubwayStrictLootProfileDefinition("Infector", 31909, 7, 3, 4, itemPoolComplete: false, new string[3] { "20260709-222339", "20260709-225408", "20260710-211430" }, new CapturedSubwayLootEvidenceDefinition[4]
		{
			new CapturedSubwayLootEvidenceDefinition(101507, 101508, 20, 1, 7, 1429),
			new CapturedSubwayLootEvidenceDefinition(101735, 101736, 21, 1, 7, 1429),
			new CapturedSubwayLootEvidenceDefinition(107491, 107492, 15, 1, 7, 1429),
			new CapturedSubwayLootEvidenceDefinition(234875, 234875, 1, 1, 7, 1429)
		}),
		new CapturedSubwayStrictLootProfileDefinition("Infected Attendant", 96056, 4, 3, 1, itemPoolComplete: false, new string[2] { "20260709-220439", "20260709-225408" }, new CapturedSubwayLootEvidenceDefinition[5]
		{
			new CapturedSubwayLootEvidenceDefinition(101695, 101696, 24, 1, 4, 2500),
			new CapturedSubwayLootEvidenceDefinition(109194, 109195, 12, 1, 4, 2500),
			new CapturedSubwayLootEvidenceDefinition(112823, 112824, 17, 1, 4, 2500),
			new CapturedSubwayLootEvidenceDefinition(234875, 234875, 1, 1, 4, 2500),
			new CapturedSubwayLootEvidenceDefinition(290619, 202727, 12, 1, 4, 2500)
		}),
		new CapturedSubwayStrictLootProfileDefinition("Lost Thought", 96193, 1, 1, 0, itemPoolComplete: false, new string[1] { "20260709-225408" }, new CapturedSubwayLootEvidenceDefinition[1]
		{
			new CapturedSubwayLootEvidenceDefinition(101675, 101676, 25, 1, 1, 10000)
		}),
		new CapturedSubwayStrictLootProfileDefinition("Uncontrollable Anger", 96195, 2, 2, 0, itemPoolComplete: false, new string[2] { "20260709-225408", "20260710-211430" }, new CapturedSubwayLootEvidenceDefinition[3]
		{
			new CapturedSubwayLootEvidenceDefinition(101809, 101810, 24, 1, 2, 5000),
			new CapturedSubwayLootEvidenceDefinition(109366, 109367, 9, 1, 2, 5000),
			new CapturedSubwayLootEvidenceDefinition(290619, 202727, 19, 1, 2, 5000)
		}),
		new CapturedSubwayStrictLootProfileDefinition("Incomplete Rebuild", 203728, 2, 2, 0, itemPoolComplete: false, new string[2] { "20260709-225408", "20260710-211430" }, new CapturedSubwayLootEvidenceDefinition[2]
		{
			new CapturedSubwayLootEvidenceDefinition(26503, 26503, 14, 1, 2, 5000),
			new CapturedSubwayLootEvidenceDefinition(142817, 142818, 16, 1, 2, 5000)
		}),
		new CapturedSubwayStrictLootProfileDefinition("Fragmented Soul", 203729, 4, 4, 0, itemPoolComplete: false, new string[1] { "20260709-225408" }, new CapturedSubwayLootEvidenceDefinition[6]
		{
			new CapturedSubwayLootEvidenceDefinition(26471, 26471, 14, 3, 4, 7500),
			new CapturedSubwayLootEvidenceDefinition(85691, 22004, 18, 1, 4, 2500),
			new CapturedSubwayLootEvidenceDefinition(85732, 21963, 17, 1, 4, 2500),
			new CapturedSubwayLootEvidenceDefinition(124304, 124305, 17, 1, 4, 2500),
			new CapturedSubwayLootEvidenceDefinition(234877, 234877, 1, 2, 4, 5000),
			new CapturedSubwayLootEvidenceDefinition(301712, 301712, 1, 1, 4, 2500)
		}),
		new CapturedSubwayStrictLootProfileDefinition("Neural Burnout", 203730, 4, 2, 2, itemPoolComplete: false, new string[4] { "20260709-225408", "20260710-211430", "20260716-034104", "20260716-221358" }, new CapturedSubwayLootEvidenceDefinition[3]
		{
			new CapturedSubwayLootEvidenceDefinition(26471, 26471, 14, 1, 4, 2500),
			new CapturedSubwayLootEvidenceDefinition(123021, 123021, 21, 1, 4, 2500),
			new CapturedSubwayLootEvidenceDefinition(124560, 124561, 16, 1, 4, 2500)
		}),
		new CapturedSubwayStrictLootProfileDefinition("Violent Vagabond", 203733, 14, 13, 1, itemPoolComplete: false, new string[4] { "20260708-143600", "20260709-210452", "20260709-225408", "20260719-020104" }, new CapturedSubwayLootEvidenceDefinition[18]
		{
			new CapturedSubwayLootEvidenceDefinition(85531, 22289, 8, 1, 14, 714),
			new CapturedSubwayLootEvidenceDefinition(122140, 122141, 7, 1, 14, 714),
			new CapturedSubwayLootEvidenceDefinition(123704, 123705, 12, 1, 14, 714),
			new CapturedSubwayLootEvidenceDefinition(124016, 124017, 6, 1, 14, 714),
			new CapturedSubwayLootEvidenceDefinition(124545, 124546, 6, 1, 14, 714),
			new CapturedSubwayLootEvidenceDefinition(128715, 128716, 6, 1, 14, 714),
			new CapturedSubwayLootEvidenceDefinition(130586, 130586, 1, 6, 14, 4286),
			new CapturedSubwayLootEvidenceDefinition(130592, 130592, 1, 2, 14, 1429),
			new CapturedSubwayLootEvidenceDefinition(130607, 130607, 1, 1, 14, 714),
			new CapturedSubwayLootEvidenceDefinition(130621, 130621, 1, 1, 14, 714),
			new CapturedSubwayLootEvidenceDefinition(152326, 152327, 6, 1, 14, 714),
			new CapturedSubwayLootEvidenceDefinition(234876, 234876, 1, 1, 14, 714),
			new CapturedSubwayLootEvidenceDefinition(234877, 234877, 1, 1, 14, 714),
			new CapturedSubwayLootEvidenceDefinition(258543, 258543, 1, 8, 14, 5714),
			new CapturedSubwayLootEvidenceDefinition(273381, 204397, 5, 1, 14, 714),
			new CapturedSubwayLootEvidenceDefinition(273381, 204397, 6, 1, 14, 714),
			new CapturedSubwayLootEvidenceDefinition(273381, 204397, 8, 1, 14, 714),
			new CapturedSubwayLootEvidenceDefinition(273381, 204397, 9, 1, 14, 714)
		}),
		new CapturedSubwayStrictLootProfileDefinition("Mugger", 203734, 18, 15, 3, itemPoolComplete: false, new string[6] { "20260708-143600", "20260709-205921", "20260709-210452", "20260709-212336", "20260710-202132", "20260719-021022" }, new CapturedSubwayLootEvidenceDefinition[22]
		{
			new CapturedSubwayLootEvidenceDefinition(25822, 25831, 5, 1, 18, 556),
			new CapturedSubwayLootEvidenceDefinition(85711, 22014, 8, 1, 18, 556),
			new CapturedSubwayLootEvidenceDefinition(123495, 123496, 5, 1, 18, 556),
			new CapturedSubwayLootEvidenceDefinition(123704, 123705, 9, 1, 18, 556),
			new CapturedSubwayLootEvidenceDefinition(123723, 123724, 6, 1, 18, 556),
			new CapturedSubwayLootEvidenceDefinition(123976, 123977, 9, 1, 18, 556),
			new CapturedSubwayLootEvidenceDefinition(124348, 124349, 7, 1, 18, 556),
			new CapturedSubwayLootEvidenceDefinition(124545, 124546, 10, 1, 18, 556),
			new CapturedSubwayLootEvidenceDefinition(128636, 128637, 8, 1, 18, 556),
			new CapturedSubwayLootEvidenceDefinition(128839, 128840, 9, 1, 18, 556),
			new CapturedSubwayLootEvidenceDefinition(130060, 130061, 5, 1, 18, 556),
			new CapturedSubwayLootEvidenceDefinition(130060, 130061, 9, 1, 18, 556),
			new CapturedSubwayLootEvidenceDefinition(131605, 131606, 7, 1, 18, 556),
			new CapturedSubwayLootEvidenceDefinition(136638, 136639, 9, 1, 18, 556),
			new CapturedSubwayLootEvidenceDefinition(136638, 136639, 12, 1, 18, 556),
			new CapturedSubwayLootEvidenceDefinition(136640, 136641, 7, 1, 18, 556),
			new CapturedSubwayLootEvidenceDefinition(136640, 136641, 8, 1, 18, 556),
			new CapturedSubwayLootEvidenceDefinition(136640, 136641, 9, 1, 18, 556),
			new CapturedSubwayLootEvidenceDefinition(136646, 136647, 9, 1, 18, 556),
			new CapturedSubwayLootEvidenceDefinition(160224, 160225, 10, 1, 18, 556),
			new CapturedSubwayLootEvidenceDefinition(234875, 234875, 1, 2, 18, 1111),
			new CapturedSubwayLootEvidenceDefinition(234876, 234876, 1, 1, 18, 556)
		}),
		new CapturedSubwayStrictLootProfileDefinition("Deranged Shopper", 203736, 2, 2, 0, itemPoolComplete: false, new string[2] { "20260708-143600", "20260709-210452" }, new CapturedSubwayLootEvidenceDefinition[2]
		{
			new CapturedSubwayLootEvidenceDefinition(123019, 123020, 6, 1, 2, 5000),
			new CapturedSubwayLootEvidenceDefinition(124465, 124466, 10, 1, 2, 5000)
		}),
		new CapturedSubwayStrictLootProfileDefinition("Stim Fiend", 203739, 13, 13, 0, itemPoolComplete: false, new string[3] { "20260708-143600", "20260709-210452", "20260709-212336" }, new CapturedSubwayLootEvidenceDefinition[17]
		{
			new CapturedSubwayLootEvidenceDefinition(102055, 102056, 11, 1, 13, 769),
			new CapturedSubwayLootEvidenceDefinition(112232, 112233, 11, 1, 13, 769),
			new CapturedSubwayLootEvidenceDefinition(234874, 234874, 1, 1, 13, 769),
			new CapturedSubwayLootEvidenceDefinition(234876, 234876, 1, 1, 13, 769),
			new CapturedSubwayLootEvidenceDefinition(234877, 234877, 1, 1, 13, 769),
			new CapturedSubwayLootEvidenceDefinition(291043, 291044, 9, 6, 13, 4615),
			new CapturedSubwayLootEvidenceDefinition(291043, 291044, 10, 2, 13, 1538),
			new CapturedSubwayLootEvidenceDefinition(291043, 291044, 11, 1, 13, 769),
			new CapturedSubwayLootEvidenceDefinition(291043, 291044, 12, 2, 13, 1538),
			new CapturedSubwayLootEvidenceDefinition(291043, 291044, 13, 1, 13, 769),
			new CapturedSubwayLootEvidenceDefinition(291043, 291044, 15, 1, 13, 769),
			new CapturedSubwayLootEvidenceDefinition(291082, 291083, 9, 6, 13, 4615),
			new CapturedSubwayLootEvidenceDefinition(291082, 291083, 10, 2, 13, 1538),
			new CapturedSubwayLootEvidenceDefinition(291082, 291083, 11, 1, 13, 769),
			new CapturedSubwayLootEvidenceDefinition(291082, 291083, 12, 2, 13, 1538),
			new CapturedSubwayLootEvidenceDefinition(291082, 291083, 13, 1, 13, 769),
			new CapturedSubwayLootEvidenceDefinition(291082, 291083, 15, 1, 13, 769)
		}),
		new CapturedSubwayStrictLootProfileDefinition("Architect Striker", 203743, 4, 3, 1, itemPoolComplete: false, new string[2] { "20260709-212336", "20260709-220439" }, new CapturedSubwayLootEvidenceDefinition[4]
		{
			new CapturedSubwayLootEvidenceDefinition(122482, 122483, 14, 1, 4, 2500),
			new CapturedSubwayLootEvidenceDefinition(124422, 124423, 13, 1, 4, 2500),
			new CapturedSubwayLootEvidenceDefinition(128890, 128891, 14, 1, 4, 2500),
			new CapturedSubwayLootEvidenceDefinition(234877, 234877, 1, 1, 4, 2500)
		}),
		new CapturedSubwayStrictLootProfileDefinition("Looter", 203745, 11, 6, 5, itemPoolComplete: false, new string[2] { "20260708-143600", "20260709-210452" }, new CapturedSubwayLootEvidenceDefinition[9]
		{
			new CapturedSubwayLootEvidenceDefinition(21605, 21605, 1, 1, 11, 909),
			new CapturedSubwayLootEvidenceDefinition(85501, 22343, 12, 1, 11, 909),
			new CapturedSubwayLootEvidenceDefinition(124422, 124422, 12, 1, 11, 909),
			new CapturedSubwayLootEvidenceDefinition(144082, 144083, 7, 1, 11, 909),
			new CapturedSubwayLootEvidenceDefinition(234874, 234874, 1, 1, 11, 909),
			new CapturedSubwayLootEvidenceDefinition(234875, 234875, 1, 1, 11, 909),
			new CapturedSubwayLootEvidenceDefinition(234877, 234877, 1, 1, 11, 909),
			new CapturedSubwayLootEvidenceDefinition(301713, 301713, 1, 1, 11, 909),
			new CapturedSubwayLootEvidenceDefinition(301714, 301714, 1, 1, 11, 909)
		}),
		new CapturedSubwayStrictLootProfileDefinition("Melded Patterns", 203747, 4, 3, 1, itemPoolComplete: false, new string[2] { "20260709-225408", "20260712-223719" }, new CapturedSubwayLootEvidenceDefinition[5]
		{
			new CapturedSubwayLootEvidenceDefinition(122672, 122673, 15, 1, 4, 2500),
			new CapturedSubwayLootEvidenceDefinition(144067, 144068, 23, 1, 4, 2500),
			new CapturedSubwayLootEvidenceDefinition(152328, 152329, 24, 1, 4, 2500),
			new CapturedSubwayLootEvidenceDefinition(234874, 234874, 1, 1, 4, 2500),
			new CapturedSubwayLootEvidenceDefinition(301710, 301710, 1, 1, 4, 2500)
		}),
		new CapturedSubwayStrictLootProfileDefinition("Workman Striker", 203854, 10, 8, 2, itemPoolComplete: false, new string[2] { "20260709-212336", "20260709-220439" }, new CapturedSubwayLootEvidenceDefinition[10]
		{
			new CapturedSubwayLootEvidenceDefinition(85562, 85561, 14, 1, 10, 1000),
			new CapturedSubwayLootEvidenceDefinition(124025, 124026, 12, 1, 10, 1000),
			new CapturedSubwayLootEvidenceDefinition(124263, 124264, 13, 1, 10, 1000),
			new CapturedSubwayLootEvidenceDefinition(130087, 130088, 16, 1, 10, 1000),
			new CapturedSubwayLootEvidenceDefinition(202719, 202720, 12, 1, 10, 1000),
			new CapturedSubwayLootEvidenceDefinition(202719, 202720, 14, 2, 10, 2000),
			new CapturedSubwayLootEvidenceDefinition(202719, 202720, 17, 1, 10, 1000),
			new CapturedSubwayLootEvidenceDefinition(234874, 234874, 1, 1, 10, 1000),
			new CapturedSubwayLootEvidenceDefinition(234877, 234877, 1, 1, 10, 1000),
			new CapturedSubwayLootEvidenceDefinition(301714, 301714, 1, 2, 10, 2000)
		}),
		new CapturedSubwayStrictLootProfileDefinition("Redundant Scan", 204178, 2, 1, 1, itemPoolComplete: false, new string[2] { "20260709-225408", "20260716-222201" }, new CapturedSubwayLootEvidenceDefinition[1]
		{
			new CapturedSubwayLootEvidenceDefinition(27263, 27263, 10, 1, 2, 5000)
		})
	};

	private static readonly CapturedSubwayOrdinaryArchetypeDefinition[] Archetypes = new CapturedSubwayOrdinaryArchetypeDefinition[20]
	{
		new CapturedSubwayOrdinaryArchetypeDefinition("shadow", "shadow", "Shadow", 30464, 150, 0, 268964353, 0, 0, 31, 0, 1227u, 0, new CapturedSubwayTextureDefinition[5]
		{
			new CapturedSubwayTextureDefinition(0, 0, 0),
			new CapturedSubwayTextureDefinition(1, 0, 0),
			new CapturedSubwayTextureDefinition(2, 0, 0),
			new CapturedSubwayTextureDefinition(3, 0, 0),
			new CapturedSubwayTextureDefinition(4, 0, 0)
		}, new CapturedSubwayMeshDefinition[0], new CapturedSubwayCombatEvidenceDefinition(observed: true, runtimeReady: true, 11, 39, 5.299336, 0, 0, 1145919558, 56), new CapturedSubwayLootEvidenceDefinition[10]
		{
			new CapturedSubwayLootEvidenceDefinition(21601, 21601, 1, 1, 15, 667),
			new CapturedSubwayLootEvidenceDefinition(27199, 27199, 10, 1, 15, 667),
			new CapturedSubwayLootEvidenceDefinition(121931, 121932, 15, 1, 15, 667),
			new CapturedSubwayLootEvidenceDefinition(122007, 122008, 12, 1, 15, 667),
			new CapturedSubwayLootEvidenceDefinition(123666, 123667, 9, 1, 15, 667),
			new CapturedSubwayLootEvidenceDefinition(124364, 124365, 10, 1, 15, 667),
			new CapturedSubwayLootEvidenceDefinition(124512, 124513, 28, 1, 15, 667),
			new CapturedSubwayLootEvidenceDefinition(152279, 152280, 18, 1, 15, 667),
			new CapturedSubwayLootEvidenceDefinition(234875, 234875, 1, 2, 15, 1333),
			new CapturedSubwayLootEvidenceDefinition(234876, 234876, 1, 1, 15, 667)
		}, new CapturedSubwayLootOutcomeEvidenceDefinition[11]
		{
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-212336", "2026-07-10T02:28:59.807757Z", "(Corpse:00F6E00F)", "(SimpleChar:79528828)", 30464, 6874, 0, 234875, 234875, 1),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-212336", "2026-07-10T02:28:59.807757Z", "(Corpse:00F6E00F)", "(SimpleChar:79528828)", 30464, 6874, 1, 124364, 124365, 10),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-212336", "2026-07-10T02:29:10.241249Z", "(Corpse:00F6E005)", "(SimpleChar:79528817)", 30464, 7127, 0, 123666, 123667, 9),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-212336", "2026-07-10T02:31:21.785932Z", "(Corpse:00F6E002)", "(SimpleChar:7953AA53)", 30464, 10177, 0, 122007, 122008, 12),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-212336", "2026-07-10T02:33:07.611309Z", "(Corpse:00F6E00F)", "(SimpleChar:7953AA33)", 30464, 11853, 0, 234875, 234875, 1),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-212336", "2026-07-10T02:33:07.611309Z", "(Corpse:00F6E00F)", "(SimpleChar:7953AA33)", 30464, 11853, 1, 27199, 27199, 10),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-212336", "2026-07-10T02:33:37.813496Z", "(Corpse:00F6E010)", "(SimpleChar:7953AA2A)", 30464, 12244, 0, 234876, 234876, 1),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-212336", "2026-07-10T02:33:37.813496Z", "(Corpse:00F6E010)", "(SimpleChar:7953AA2A)", 30464, 12244, 1, 121931, 121932, 15),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260712-223719", "2026-07-13T03:39:35.6502502Z", "(Corpse:00F6C011)", "(SimpleChar:79607876)", 30464, 2914, 0, 152279, 152280, 18),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260712-223719", "2026-07-13T03:39:37.0377259Z", "(Corpse:00F6C004)", "(SimpleChar:79607875)", 30464, 2941, 0, 124512, 124513, 28),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260712-223719", "2026-07-13T03:39:52.8441413Z", "(Corpse:00F6C007)", "(SimpleChar:79607838)", 30464, 3180, 0, 21601, 21601, 1)
		}, new CapturedSubwayCorpseEvidenceDefinition[20]
		{
			new CapturedSubwayCorpseEvidenceDefinition("20260709-212336", "2026-07-10T02:28:25.7622393Z", "(Corpse:00F6E00C)", "(SimpleChar:79528829)", 10, 30464, 30434, 59),
			new CapturedSubwayCorpseEvidenceDefinition("20260709-212336", "2026-07-10T02:28:36.4939023Z", "(Corpse:00F6E004)", "(SimpleChar:7952882A)", 9, 30464, 30434, 53),
			new CapturedSubwayCorpseEvidenceDefinition("20260709-212336", "2026-07-10T02:28:53.4426925Z", "(Corpse:00F6E00F)", "(SimpleChar:79528828)", 10, 30464, 30434, 59),
			new CapturedSubwayCorpseEvidenceDefinition("20260709-212336", "2026-07-10T02:29:03.6073827Z", "(Corpse:00F6E005)", "(SimpleChar:79528817)", 10, 30464, 30434, 59),
			new CapturedSubwayCorpseEvidenceDefinition("20260709-212336", "2026-07-10T02:29:12.5751669Z", "(Corpse:00F6E006)", "(SimpleChar:7952880B)", 9, 30464, 30434, 53),
			new CapturedSubwayCorpseEvidenceDefinition("20260709-212336", "2026-07-10T02:30:24.1547653Z", "(Corpse:00F6E002)", "(SimpleChar:7953AA55)", 9, 30464, 30434, 53),
			new CapturedSubwayCorpseEvidenceDefinition("20260709-212336", "2026-07-10T02:30:39.5878831Z", "(Corpse:00F6E002)", "(SimpleChar:7953AA56)", 11, 30464, 30434, 66),
			new CapturedSubwayCorpseEvidenceDefinition("20260709-212336", "2026-07-10T02:31:01.5684243Z", "(Corpse:00F6E00B)", "(SimpleChar:7953AA1C)", 10, 30464, 30434, 59),
			new CapturedSubwayCorpseEvidenceDefinition("20260709-212336", "2026-07-10T02:31:18.8360694Z", "(Corpse:00F6E002)", "(SimpleChar:7953AA53)", 10, 30464, 30434, 59),
			new CapturedSubwayCorpseEvidenceDefinition("20260709-212336", "2026-07-10T02:32:30.6241496Z", "(Corpse:00F6E005)", "(SimpleChar:7953AA2B)", 15, 30464, 30434, 92),
			new CapturedSubwayCorpseEvidenceDefinition("20260709-212336", "2026-07-10T02:33:04.8836498Z", "(Corpse:00F6E00F)", "(SimpleChar:7953AA33)", 14, 30464, 30434, 85),
			new CapturedSubwayCorpseEvidenceDefinition("20260709-212336", "2026-07-10T02:33:36.1981049Z", "(Corpse:00F6E010)", "(SimpleChar:7953AA2A)", 13, 30464, 30434, 79),
			new CapturedSubwayCorpseEvidenceDefinition("20260709-220439", "2026-07-10T03:10:29.5006422Z", "(Corpse:00F6E00D)", "(SimpleChar:7953A97A)", 14, 30464, 30434, 85),
			new CapturedSubwayCorpseEvidenceDefinition("20260709-220439", "2026-07-10T03:10:33.1638663Z", "(Corpse:00F6E011)", "(SimpleChar:7953A96C)", 15, 30464, 30434, 92),
			new CapturedSubwayCorpseEvidenceDefinition("20260709-225408", "2026-07-10T04:04:50.2955408Z", "(Corpse:00F6E00A)", "(SimpleChar:7953AFF7)", 21, 30464, 30434, 131),
			new CapturedSubwayCorpseEvidenceDefinition("20260712-223719", "2026-07-13T03:38:49.5373589Z", "(Corpse:00F6C004)", "(SimpleChar:79607875)", 23, 30464, 30434, 144),
			new CapturedSubwayCorpseEvidenceDefinition("20260712-223719", "2026-07-13T03:39:03.3427964Z", "(Corpse:00F6C011)", "(SimpleChar:79607876)", 22, 30464, 30434, 137),
			new CapturedSubwayCorpseEvidenceDefinition("20260712-223719", "2026-07-13T03:39:06.9526642Z", "(Corpse:00F6C012)", "(SimpleChar:79607877)", 22, 30464, 30434, 137),
			new CapturedSubwayCorpseEvidenceDefinition("20260712-223719", "2026-07-13T03:39:33.0802477Z", "(Corpse:00F6C007)", "(SimpleChar:79607838)", 23, 30464, 30434, 144),
			new CapturedSubwayCorpseEvidenceDefinition("20260716-215947", "2026-07-17T03:00:52.4977971Z", "(Corpse:00F69001)", "(SimpleChar:79702492)", 23, 30464, 30434, 144)
		}, new string[12]
		{
			"20260709-205921", "20260709-210452", "20260709-212115", "20260709-212336", "20260709-220439", "20260709-222339", "20260709-225408", "20260710-211430", "20260712-223719", "20260716-033326",
			"20260716-034104", "20260716-215947"
		}),
		new CapturedSubwayOrdinaryArchetypeDefinition("stim_fiend", "stim_fiend", "Stim Fiend", 203739, 138, 0, 268964353, 0, 0, 31, 0, 1579u, 40693, new CapturedSubwayTextureDefinition[5]
		{
			new CapturedSubwayTextureDefinition(0, 117653, 0),
			new CapturedSubwayTextureDefinition(1, 40898, 0),
			new CapturedSubwayTextureDefinition(2, 40903, 0),
			new CapturedSubwayTextureDefinition(3, 87442, 0),
			new CapturedSubwayTextureDefinition(4, 40907, 0)
		}, new CapturedSubwayMeshDefinition[1]
		{
			new CapturedSubwayMeshDefinition(0, 40693u, 0, 4)
		}, new CapturedSubwayCombatEvidenceDefinition(observed: true, runtimeReady: true, 10, 16, 5.666535, 0, 0, 1397315377, 13), new CapturedSubwayLootEvidenceDefinition[17]
		{
			new CapturedSubwayLootEvidenceDefinition(102055, 102056, 11, 1, 13, 769),
			new CapturedSubwayLootEvidenceDefinition(112232, 112233, 11, 1, 13, 769),
			new CapturedSubwayLootEvidenceDefinition(234874, 234874, 1, 1, 13, 769),
			new CapturedSubwayLootEvidenceDefinition(234876, 234876, 1, 1, 13, 769),
			new CapturedSubwayLootEvidenceDefinition(234877, 234877, 1, 1, 13, 769),
			new CapturedSubwayLootEvidenceDefinition(291043, 291044, 9, 6, 13, 4615),
			new CapturedSubwayLootEvidenceDefinition(291043, 291044, 10, 2, 13, 1538),
			new CapturedSubwayLootEvidenceDefinition(291043, 291044, 11, 1, 13, 769),
			new CapturedSubwayLootEvidenceDefinition(291043, 291044, 12, 2, 13, 1538),
			new CapturedSubwayLootEvidenceDefinition(291043, 291044, 13, 1, 13, 769),
			new CapturedSubwayLootEvidenceDefinition(291043, 291044, 15, 1, 13, 769),
			new CapturedSubwayLootEvidenceDefinition(291082, 291083, 9, 6, 13, 4615),
			new CapturedSubwayLootEvidenceDefinition(291082, 291083, 10, 2, 13, 1538),
			new CapturedSubwayLootEvidenceDefinition(291082, 291083, 11, 1, 13, 769),
			new CapturedSubwayLootEvidenceDefinition(291082, 291083, 12, 2, 13, 1538),
			new CapturedSubwayLootEvidenceDefinition(291082, 291083, 13, 1, 13, 769),
			new CapturedSubwayLootEvidenceDefinition(291082, 291083, 15, 1, 13, 769)
		}, new CapturedSubwayLootOutcomeEvidenceDefinition[31]
		{
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260708-143600", "2026-07-08T19:56:47.7629790Z", "(Corpse:00F6E002)", "(SimpleChar:794CD773)", 203739, 17534, 0, 291082, 291083, 9),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260708-143600", "2026-07-08T19:56:47.7629790Z", "(Corpse:00F6E002)", "(SimpleChar:794CD773)", 203739, 17534, 1, 291043, 291044, 9),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260708-143600", "2026-07-08T19:58:27.8155839Z", "(Corpse:00F6E001)", "(SimpleChar:794CD77C)", 203739, 18001, 0, 234877, 234877, 1),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260708-143600", "2026-07-08T19:58:27.8155839Z", "(Corpse:00F6E001)", "(SimpleChar:794CD77C)", 203739, 18001, 1, 291082, 291083, 9),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260708-143600", "2026-07-08T19:58:27.8155839Z", "(Corpse:00F6E001)", "(SimpleChar:794CD77C)", 203739, 18001, 2, 291043, 291044, 9),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260708-143600", "2026-07-08T19:59:11.1652424Z", "(Corpse:00F6E00A)", "(SimpleChar:794CD779)", 203739, 18393, 0, 291082, 291083, 9),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260708-143600", "2026-07-08T19:59:11.1652424Z", "(Corpse:00F6E00A)", "(SimpleChar:794CD779)", 203739, 18393, 1, 291043, 291044, 9),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260708-143600", "2026-07-08T20:01:08.6766588Z", "(Corpse:00F6E00C)", "(SimpleChar:794CD778)", 203739, 19326, 0, 112232, 112233, 11),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260708-143600", "2026-07-08T20:01:08.6766588Z", "(Corpse:00F6E00C)", "(SimpleChar:794CD778)", 203739, 19326, 1, 291082, 291083, 9),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260708-143600", "2026-07-08T20:01:08.6766588Z", "(Corpse:00F6E00C)", "(SimpleChar:794CD778)", 203739, 19326, 2, 291043, 291044, 9),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260708-143600", "2026-07-08T20:01:11.5463949Z", "(Corpse:00F6E012)", "(SimpleChar:794CD78A)", 203739, 19346, 0, 102055, 102056, 11),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260708-143600", "2026-07-08T20:01:11.5463949Z", "(Corpse:00F6E012)", "(SimpleChar:794CD78A)", 203739, 19346, 1, 291082, 291083, 12),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260708-143600", "2026-07-08T20:01:11.5463949Z", "(Corpse:00F6E012)", "(SimpleChar:794CD78A)", 203739, 19346, 2, 291043, 291044, 12),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260708-143600", "2026-07-08T20:03:30.8256228Z", "(Corpse:00F6E01B)", "(SimpleChar:794CD78D)", 203739, 20291, 0, 291082, 291083, 15),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260708-143600", "2026-07-08T20:03:30.8256228Z", "(Corpse:00F6E01B)", "(SimpleChar:794CD78D)", 203739, 20291, 1, 291043, 291044, 15),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-210452", "2026-07-10T02:13:32.149825Z", "(Corpse:00F6E015)", "(SimpleChar:7953AD99)", 203739, 7707, 0, 234874, 234874, 1),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-210452", "2026-07-10T02:13:32.149825Z", "(Corpse:00F6E015)", "(SimpleChar:7953AD99)", 203739, 7707, 1, 291082, 291083, 12),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-210452", "2026-07-10T02:13:32.149825Z", "(Corpse:00F6E015)", "(SimpleChar:7953AD99)", 203739, 7707, 2, 291043, 291044, 12),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-210452", "2026-07-10T02:13:48.548937Z", "(Corpse:00F6E017)", "(SimpleChar:7953ADBE)", 203739, 7856, 0, 234876, 234876, 1),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-210452", "2026-07-10T02:13:48.548937Z", "(Corpse:00F6E017)", "(SimpleChar:7953ADBE)", 203739, 7856, 1, 291082, 291083, 10),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-210452", "2026-07-10T02:13:48.548937Z", "(Corpse:00F6E017)", "(SimpleChar:7953ADBE)", 203739, 7856, 2, 291043, 291044, 10),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-210452", "2026-07-10T02:14:44.095961Z", "(Corpse:00F6E00C)", "(SimpleChar:7953ADB1)", 203739, 8294, 0, 291082, 291083, 9),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-210452", "2026-07-10T02:14:44.095961Z", "(Corpse:00F6E00C)", "(SimpleChar:7953ADB1)", 203739, 8294, 1, 291043, 291044, 9),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-210452", "2026-07-10T02:15:26.194874Z", "(Corpse:00F6E02D)", "(SimpleChar:795312BD)", 203739, 8603, 0, 291082, 291083, 9),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-210452", "2026-07-10T02:15:26.194874Z", "(Corpse:00F6E02D)", "(SimpleChar:795312BD)", 203739, 8603, 1, 291043, 291044, 9),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-210452", "2026-07-10T02:15:51.890356Z", "(Corpse:00F6E031)", "(SimpleChar:7953ADAB)", 203739, 8752, 0, 291082, 291083, 11),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-210452", "2026-07-10T02:15:51.890356Z", "(Corpse:00F6E031)", "(SimpleChar:7953ADAB)", 203739, 8752, 1, 291043, 291044, 11),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-210452", "2026-07-10T02:16:14.772371Z", "(Corpse:00F6E011)", "(SimpleChar:795312C9)", 203739, 8874, 0, 291082, 291083, 13),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-210452", "2026-07-10T02:16:14.772371Z", "(Corpse:00F6E011)", "(SimpleChar:795312C9)", 203739, 8874, 1, 291043, 291044, 13),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-212336", "2026-07-10T02:31:56.719615Z", "(Corpse:00F6E00E)", "(SimpleChar:7953AA4B)", 203739, 10855, 0, 291082, 291083, 10),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-212336", "2026-07-10T02:31:56.719615Z", "(Corpse:00F6E00E)", "(SimpleChar:7953AA4B)", 203739, 10855, 1, 291043, 291044, 10)
		}, new CapturedSubwayCorpseEvidenceDefinition[15]
		{
			new CapturedSubwayCorpseEvidenceDefinition("20260708-143600", "2026-07-08T19:56:44.5145829Z", "(Corpse:00F6E002)", "(SimpleChar:794CD773)", 10, 203739, 5907, 59),
			new CapturedSubwayCorpseEvidenceDefinition("20260708-143600", "2026-07-08T19:58:25.3361948Z", "(Corpse:00F6E001)", "(SimpleChar:794CD77C)", 11, 203739, 5907, 66),
			new CapturedSubwayCorpseEvidenceDefinition("20260708-143600", "2026-07-08T19:59:07.4068488Z", "(Corpse:00F6E00A)", "(SimpleChar:794CD779)", 10, 203739, 5907, 59),
			new CapturedSubwayCorpseEvidenceDefinition("20260708-143600", "2026-07-08T20:00:11.0166796Z", "(Corpse:00F6E00C)", "(SimpleChar:794CD778)", 12, 203739, 5907, 72),
			new CapturedSubwayCorpseEvidenceDefinition("20260708-143600", "2026-07-08T20:00:59.5554387Z", "(Corpse:00F6E012)", "(SimpleChar:794CD78A)", 10, 203739, 5907, 59),
			new CapturedSubwayCorpseEvidenceDefinition("20260708-143600", "2026-07-08T20:03:10.0339591Z", "(Corpse:00F6E01B)", "(SimpleChar:794CD78D)", 12, 203739, 5907, 72),
			new CapturedSubwayCorpseEvidenceDefinition("20260709-210452", "2026-07-10T02:13:28.0682119Z", "(Corpse:00F6E015)", "(SimpleChar:7953AD99)", 12, 203739, 5907, 72),
			new CapturedSubwayCorpseEvidenceDefinition("20260709-210452", "2026-07-10T02:13:45.8173652Z", "(Corpse:00F6E017)", "(SimpleChar:7953ADBE)", 10, 203739, 5907, 59),
			new CapturedSubwayCorpseEvidenceDefinition("20260709-210452", "2026-07-10T02:14:38.5477379Z", "(Corpse:00F6E00C)", "(SimpleChar:7953ADB1)", 11, 203739, 5907, 66),
			new CapturedSubwayCorpseEvidenceDefinition("20260709-210452", "2026-07-10T02:15:23.2291153Z", "(Corpse:00F6E02D)", "(SimpleChar:795312BD)", 10, 203739, 5907, 59),
			new CapturedSubwayCorpseEvidenceDefinition("20260709-210452", "2026-07-10T02:15:41.0371834Z", "(Corpse:00F6E031)", "(SimpleChar:7953ADAB)", 10, 203739, 5907, 59),
			new CapturedSubwayCorpseEvidenceDefinition("20260709-210452", "2026-07-10T02:16:11.7747284Z", "(Corpse:00F6E011)", "(SimpleChar:795312C9)", 12, 203739, 5907, 72),
			new CapturedSubwayCorpseEvidenceDefinition("20260709-212336", "2026-07-10T02:31:54.5261702Z", "(Corpse:00F6E00E)", "(SimpleChar:7953AA4B)", 13, 203739, 5907, 79),
			new CapturedSubwayCorpseEvidenceDefinition("20260709-220439", "2026-07-10T03:07:15.1584598Z", "(Corpse:00F6E00D)", "(SimpleChar:7953ABAF)", 14, 203739, 5907, 85),
			new CapturedSubwayCorpseEvidenceDefinition("20260709-225408", "2026-07-10T03:55:54.4639974Z", "(Corpse:00F6E004)", "(SimpleChar:7954530F)", 13, 203739, 5907, 79)
		}, new string[10] { "20260708-143600", "20260709-205921", "20260709-210452", "20260709-212115", "20260709-212336", "20260709-220439", "20260709-222339", "20260709-225408", "20260710-202132", "20260710-211430" }),
		new CapturedSubwayOrdinaryArchetypeDefinition("workman_striker", "striker", "Workman Striker", 203854, 149, 0, 268964353, 0, 0, 31, 1, 1419u, 40127, new CapturedSubwayTextureDefinition[5]
		{
			new CapturedSubwayTextureDefinition(0, 0, 0),
			new CapturedSubwayTextureDefinition(1, 22562, 0),
			new CapturedSubwayTextureDefinition(2, 0, 0),
			new CapturedSubwayTextureDefinition(3, 22537, 0),
			new CapturedSubwayTextureDefinition(4, 22618, 0)
		}, new CapturedSubwayMeshDefinition[3]
		{
			new CapturedSubwayMeshDefinition(0, 20007u, 22650, 2),
			new CapturedSubwayMeshDefinition(0, 40127u, 0, 4),
			new CapturedSubwayMeshDefinition(1, 30235u, 0, 2)
		}, new CapturedSubwayCombatEvidenceDefinition(observed: true, runtimeReady: true, 14, 23, 5.092328, 6, 0, 0, 47), new CapturedSubwayLootEvidenceDefinition[10]
		{
			new CapturedSubwayLootEvidenceDefinition(85562, 85561, 14, 1, 10, 1000),
			new CapturedSubwayLootEvidenceDefinition(124025, 124026, 12, 1, 10, 1000),
			new CapturedSubwayLootEvidenceDefinition(124263, 124264, 13, 1, 10, 1000),
			new CapturedSubwayLootEvidenceDefinition(130087, 130088, 16, 1, 10, 1000),
			new CapturedSubwayLootEvidenceDefinition(202719, 202720, 12, 1, 10, 1000),
			new CapturedSubwayLootEvidenceDefinition(202719, 202720, 14, 2, 10, 2000),
			new CapturedSubwayLootEvidenceDefinition(202719, 202720, 17, 1, 10, 1000),
			new CapturedSubwayLootEvidenceDefinition(234874, 234874, 1, 1, 10, 1000),
			new CapturedSubwayLootEvidenceDefinition(234877, 234877, 1, 1, 10, 1000),
			new CapturedSubwayLootEvidenceDefinition(301714, 301714, 1, 2, 10, 2000)
		}, new CapturedSubwayLootOutcomeEvidenceDefinition[12]
		{
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-212336", "2026-07-10T02:34:29.929105Z", "(Corpse:00F6E002)", "(SimpleChar:7953AA77)", 203854, 12913, 0, 234877, 234877, 1),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-212336", "2026-07-10T02:34:29.929105Z", "(Corpse:00F6E002)", "(SimpleChar:7953AA77)", 203854, 12913, 1, 130087, 130088, 16),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-212336", "2026-07-10T02:35:36.077246Z", "(Corpse:00F6E009)", "(SimpleChar:7953AB03)", 203854, 13569, 0, 202719, 202720, 17),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-212336", "2026-07-10T02:35:36.077246Z", "(Corpse:00F6E009)", "(SimpleChar:7953AB03)", 203854, 13569, 1, 124025, 124026, 12),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-220439", "2026-07-10T03:07:54.540987Z", "(Corpse:00F6E005)", "(SimpleChar:7953A9F7)", 203854, 7367, 0, 202719, 202720, 14),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-220439", "2026-07-10T03:09:04.556925Z", "(Corpse:00F6E007)", "(SimpleChar:7953AA0D)", 203854, 8791, 0, 234874, 234874, 1),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-220439", "2026-07-10T03:09:04.556925Z", "(Corpse:00F6E007)", "(SimpleChar:7953AA0D)", 203854, 8791, 1, 124263, 124264, 13),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-220439", "2026-07-10T03:09:47.502632Z", "(Corpse:00F6E002)", "(SimpleChar:7953AE95)", 203854, 10026, 0, 202719, 202720, 14),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-220439", "2026-07-10T03:13:22.690754Z", "(Corpse:00F6E007)", "(SimpleChar:7953AAE9)", 203854, 12289, 0, 301714, 301714, 1),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-220439", "2026-07-10T03:14:58.228688Z", "(Corpse:00F6E003)", "(SimpleChar:7953AFB8)", 203854, 13430, 0, 202719, 202720, 12),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-220439", "2026-07-10T03:14:58.228688Z", "(Corpse:00F6E003)", "(SimpleChar:7953AFB8)", 203854, 13430, 1, 85562, 85561, 14),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-220439", "2026-07-10T03:15:40.403059Z", "(Corpse:00F6E004)", "(SimpleChar:79545000)", 203854, 13685, 0, 301714, 301714, 1)
		}, new CapturedSubwayCorpseEvidenceDefinition[20]
		{
			new CapturedSubwayCorpseEvidenceDefinition("20260709-212336", "2026-07-10T02:29:58.0753630Z", "(Corpse:00F6E002)", "(SimpleChar:7953AA16)", 14, 203854, 17899, 85),
			new CapturedSubwayCorpseEvidenceDefinition("20260709-212336", "2026-07-10T02:34:23.6423824Z", "(Corpse:00F6E002)", "(SimpleChar:7953AA77)", 15, 203854, 17899, 92),
			new CapturedSubwayCorpseEvidenceDefinition("20260709-212336", "2026-07-10T02:35:32.9898774Z", "(Corpse:00F6E009)", "(SimpleChar:7953AB03)", 14, 203854, 17899, 85),
			new CapturedSubwayCorpseEvidenceDefinition("20260709-212336", "2026-07-10T02:36:15.8378611Z", "(Corpse:00F6E005)", "(SimpleChar:7953AABE)", 14, 203854, 17899, 85),
			new CapturedSubwayCorpseEvidenceDefinition("20260709-220439", "2026-07-10T03:07:47.5912322Z", "(Corpse:00F6E005)", "(SimpleChar:7953A9F7)", 13, 203854, 17899, 79),
			new CapturedSubwayCorpseEvidenceDefinition("20260709-220439", "2026-07-10T03:08:59.6548295Z", "(Corpse:00F6E007)", "(SimpleChar:7953AA0D)", 15, 203854, 17899, 92),
			new CapturedSubwayCorpseEvidenceDefinition("20260709-220439", "2026-07-10T03:09:19.1039309Z", "(Corpse:00F6E01F)", "(SimpleChar:7953A84F)", 17, 203854, 17899, 105),
			new CapturedSubwayCorpseEvidenceDefinition("20260709-220439", "2026-07-10T03:09:27.9702512Z", "(Corpse:00F6E021)", "(SimpleChar:7953A830)", 14, 203854, 17899, 85),
			new CapturedSubwayCorpseEvidenceDefinition("20260709-220439", "2026-07-10T03:09:42.6372333Z", "(Corpse:00F6E002)", "(SimpleChar:7953AE95)", 16, 203854, 17899, 98),
			new CapturedSubwayCorpseEvidenceDefinition("20260709-220439", "2026-07-10T03:12:24.0205269Z", "(Corpse:00F6E014)", "(SimpleChar:79545136)", 13, 203854, 17899, 79),
			new CapturedSubwayCorpseEvidenceDefinition("20260709-220439", "2026-07-10T03:12:38.8595811Z", "(Corpse:00F6E007)", "(SimpleChar:7953AAE9)", 14, 203854, 17899, 85),
			new CapturedSubwayCorpseEvidenceDefinition("20260709-220439", "2026-07-10T03:14:53.4718416Z", "(Corpse:00F6E003)", "(SimpleChar:7953AFB8)", 14, 203854, 17899, 85),
			new CapturedSubwayCorpseEvidenceDefinition("20260709-220439", "2026-07-10T03:15:36.5036215Z", "(Corpse:00F6E004)", "(SimpleChar:79545000)", 16, 203854, 17899, 98),
			new CapturedSubwayCorpseEvidenceDefinition("20260709-222339", "2026-07-10T03:26:08.5092774Z", "(Corpse:00F6E013)", "(SimpleChar:79545224)", 16, 203854, 17899, 98),
			new CapturedSubwayCorpseEvidenceDefinition("20260709-222339", "2026-07-10T03:26:26.9917448Z", "(Corpse:00F6E002)", "(SimpleChar:79545219)", 16, 203854, 17899, 98),
			new CapturedSubwayCorpseEvidenceDefinition("20260709-222339", "2026-07-10T03:26:38.5907959Z", "(Corpse:00F6E00F)", "(SimpleChar:79545216)", 17, 203854, 17899, 105),
			new CapturedSubwayCorpseEvidenceDefinition("20260709-222339", "2026-07-10T03:28:03.7705224Z", "(Corpse:00F6E00A)", "(SimpleChar:7953A9F0)", 15, 203854, 17899, 92),
			new CapturedSubwayCorpseEvidenceDefinition("20260709-225408", "2026-07-10T03:56:49.4276621Z", "(Corpse:00F6E01C)", "(SimpleChar:7954531C)", 17, 203854, 17899, 105),
			new CapturedSubwayCorpseEvidenceDefinition("20260709-225408", "2026-07-10T03:56:58.2600884Z", "(Corpse:00F6E012)", "(SimpleChar:79545313)", 14, 203854, 17899, 85),
			new CapturedSubwayCorpseEvidenceDefinition("20260709-225408", "2026-07-10T04:04:19.7911579Z", "(Corpse:00F6E007)", "(SimpleChar:795451CA)", 25, 203854, 17899, 156)
		}, new string[10] { "20260709-205921", "20260709-212115", "20260709-212336", "20260709-213711", "20260709-220439", "20260709-222339", "20260709-225408", "20260710-211430", "20260716-033326", "20260716-034104" }, new CapturedSubwaySourceWeaponEvidenceDefinition[21]
		{
			new CapturedSubwaySourceWeaponEvidenceDefinition(2035525711, 122905, 122906, 19, "20260709-212115,20260709-212336,20260709-220439"),
			new CapturedSubwaySourceWeaponEvidenceDefinition(2035526128, 122905, 122906, 17, "20260709-212115,20260709-212336,20260709-220439"),
			new CapturedSubwaySourceWeaponEvidenceDefinition(2035526157, 122905, 122906, 18, "20260709-212115,20260709-212336,20260709-220439"),
			new CapturedSubwaySourceWeaponEvidenceDefinition(2035526166, 122905, 122906, 15, "20260709-205921,20260709-212115,20260709-212336"),
			new CapturedSubwaySourceWeaponEvidenceDefinition(2035526263, 122905, 122906, 14, "20260709-212115,20260709-212336"),
			new CapturedSubwaySourceWeaponEvidenceDefinition(2035526334, 122905, 122906, 13, "20260709-212115,20260709-212336"),
			new CapturedSubwaySourceWeaponEvidenceDefinition(2035526377, 122905, 122906, 14, "20260709-212115,20260709-212336,20260709-220439"),
			new CapturedSubwaySourceWeaponEvidenceDefinition(2035526403, 122905, 122906, 16, "20260709-212115,20260709-212336"),
			new CapturedSubwaySourceWeaponEvidenceDefinition(2035527573, 122905, 122906, 12, "20260709-212115,20260709-212336,20260709-220439,20260709-222339"),
			new CapturedSubwaySourceWeaponEvidenceDefinition(2035527608, 122905, 122906, 17, "20260709-212115,20260709-212336,20260709-220439"),
			new CapturedSubwaySourceWeaponEvidenceDefinition(2035527612, 122905, 122906, 19, "20260709-212115,20260709-212336"),
			new CapturedSubwaySourceWeaponEvidenceDefinition(2035527645, 122905, 122906, 12, "20260709-212115,20260709-212336"),
			new CapturedSubwaySourceWeaponEvidenceDefinition(2035527673, 122905, 122906, 16, "20260709-212115,20260709-212336,20260709-220439,20260709-222339"),
			new CapturedSubwaySourceWeaponEvidenceDefinition(2035568640, 122906, 122906, 20, "20260709-212115,20260709-212336,20260709-220439"),
			new CapturedSubwaySourceWeaponEvidenceDefinition(2035568666, 122905, 122906, 16, "20260709-212115,20260709-212336,20260709-220439,20260709-222339"),
			new CapturedSubwaySourceWeaponEvidenceDefinition(2035568904, 122905, 122906, 15, "20260709-212115,20260709-212336,20260709-213711,20260709-220439"),
			new CapturedSubwaySourceWeaponEvidenceDefinition(2035569098, 122907, 122908, 27, "20260709-222339,20260709-225408"),
			new CapturedSubwaySourceWeaponEvidenceDefinition(2035569157, 122905, 122906, 11, "20260709-222339"),
			new CapturedSubwaySourceWeaponEvidenceDefinition(2035569171, 122905, 122906, 14, "20260709-222339,20260709-225408"),
			new CapturedSubwaySourceWeaponEvidenceDefinition(2035569177, 122905, 122906, 19, "20260709-222339"),
			new CapturedSubwaySourceWeaponEvidenceDefinition(2035569188, 122905, 122906, 14, "20260709-222339")
		}),
		new CapturedSubwayOrdinaryArchetypeDefinition("architect_striker", "striker", "Architect Striker", 203743, 149, 0, 268964353, 0, 0, 31, 1, 1579u, 40698, new CapturedSubwayTextureDefinition[5]
		{
			new CapturedSubwayTextureDefinition(0, 40976, 0),
			new CapturedSubwayTextureDefinition(1, 22562, 0),
			new CapturedSubwayTextureDefinition(2, 40968, 0),
			new CapturedSubwayTextureDefinition(3, 22537, 0),
			new CapturedSubwayTextureDefinition(4, 22618, 0)
		}, new CapturedSubwayMeshDefinition[1]
		{
			new CapturedSubwayMeshDefinition(0, 40698u, 0, 4)
		}, new CapturedSubwayCombatEvidenceDefinition(observed: true, runtimeReady: true, 13, 17, 5.42542, 0, 0, 1397315377, 15), new CapturedSubwayLootEvidenceDefinition[4]
		{
			new CapturedSubwayLootEvidenceDefinition(122482, 122483, 14, 1, 4, 2500),
			new CapturedSubwayLootEvidenceDefinition(124422, 124423, 13, 1, 4, 2500),
			new CapturedSubwayLootEvidenceDefinition(128890, 128891, 14, 1, 4, 2500),
			new CapturedSubwayLootEvidenceDefinition(234877, 234877, 1, 1, 4, 2500)
		}, new CapturedSubwayLootOutcomeEvidenceDefinition[4]
		{
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-220439", "2026-07-10T03:13:25.125650Z", "(Corpse:00F6E015)", "(SimpleChar:7953A9B6)", 203743, 12314, 0, 128890, 128891, 14),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-220439", "2026-07-10T03:13:27.109576Z", "(Corpse:00F6E004)", "(SimpleChar:7953A9B3)", 203743, 12334, 0, 124422, 124423, 13),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-220439", "2026-07-10T03:13:29.025371Z", "(Corpse:00F6E016)", "(SimpleChar:7953AAEB)", 203743, 12359, 0, 234877, 234877, 1),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-220439", "2026-07-10T03:13:29.025371Z", "(Corpse:00F6E016)", "(SimpleChar:7953AAEB)", 203743, 12359, 1, 122482, 122483, 14)
		}, new CapturedSubwayCorpseEvidenceDefinition[4]
		{
			new CapturedSubwayCorpseEvidenceDefinition("20260709-212336", "2026-07-10T02:34:59.2293267Z", "(Corpse:00F6E004)", "(SimpleChar:7953A9BD)", 13, 203743, 17870, 79),
			new CapturedSubwayCorpseEvidenceDefinition("20260709-220439", "2026-07-10T03:12:35.4312488Z", "(Corpse:00F6E004)", "(SimpleChar:7953A9B3)", 15, 203743, 17870, 92),
			new CapturedSubwayCorpseEvidenceDefinition("20260709-220439", "2026-07-10T03:12:42.4436898Z", "(Corpse:00F6E015)", "(SimpleChar:7953A9B6)", 13, 203743, 17870, 79),
			new CapturedSubwayCorpseEvidenceDefinition("20260709-220439", "2026-07-10T03:12:45.3434043Z", "(Corpse:00F6E016)", "(SimpleChar:7953AAEB)", 14, 203743, 17870, 85)
		}, new string[7] { "20260709-212115", "20260709-212336", "20260709-213711", "20260709-220439", "20260709-222339", "20260709-225408", "20260710-211430" }),
		new CapturedSubwayOrdinaryArchetypeDefinition("infected_attendant", "infected_attendant", "Infected Attendant", 96056, 138, 0, 268964353, 0, 0, 31, 0, 1227u, 0, new CapturedSubwayTextureDefinition[5]
		{
			new CapturedSubwayTextureDefinition(0, 0, 0),
			new CapturedSubwayTextureDefinition(1, 0, 0),
			new CapturedSubwayTextureDefinition(2, 0, 0),
			new CapturedSubwayTextureDefinition(3, 0, 0),
			new CapturedSubwayTextureDefinition(4, 0, 0)
		}, new CapturedSubwayMeshDefinition[0], new CapturedSubwayCombatEvidenceDefinition(observed: true, runtimeReady: false, 11, 11, 0.0, 0, 0, 1397315377, 1), new CapturedSubwayLootEvidenceDefinition[5]
		{
			new CapturedSubwayLootEvidenceDefinition(101695, 101696, 24, 1, 4, 2500),
			new CapturedSubwayLootEvidenceDefinition(109194, 109195, 12, 1, 4, 2500),
			new CapturedSubwayLootEvidenceDefinition(112823, 112824, 17, 1, 4, 2500),
			new CapturedSubwayLootEvidenceDefinition(234875, 234875, 1, 1, 4, 2500),
			new CapturedSubwayLootEvidenceDefinition(290619, 202727, 12, 1, 4, 2500)
		}, new CapturedSubwayLootOutcomeEvidenceDefinition[5]
		{
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-220439", "2026-07-10T03:06:54.895918Z", "(Corpse:00F6E00C)", "(SimpleChar:7953AB2D)", 96056, 6038, 0, 109194, 109195, 12),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-220439", "2026-07-10T03:13:47.740262Z", "(Corpse:00F6E002)", "(SimpleChar:7953AA32)", 96056, 12557, 0, 290619, 202727, 12),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-220439", "2026-07-10T03:13:47.740262Z", "(Corpse:00F6E002)", "(SimpleChar:7953AA32)", 96056, 12557, 1, 112823, 112824, 17),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-225408", "2026-07-10T04:04:32.692834Z", "(Corpse:00F6E023)", "(SimpleChar:795451AC)", 96056, 15073, 0, 234875, 234875, 1),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-225408", "2026-07-10T04:04:32.692834Z", "(Corpse:00F6E023)", "(SimpleChar:795451AC)", 96056, 15073, 1, 101695, 101696, 24)
		}, new CapturedSubwayCorpseEvidenceDefinition[6]
		{
			new CapturedSubwayCorpseEvidenceDefinition("20260709-220439", "2026-07-10T03:06:48.0775512Z", "(Corpse:00F6E00B)", "(SimpleChar:7953AA1A)", 11, 96056, 96024, 14),
			new CapturedSubwayCorpseEvidenceDefinition("20260709-220439", "2026-07-10T03:06:52.8439919Z", "(Corpse:00F6E00C)", "(SimpleChar:7953AB2D)", 11, 96056, 96024, 14),
			new CapturedSubwayCorpseEvidenceDefinition("20260709-220439", "2026-07-10T03:07:33.3925860Z", "(Corpse:00F6E003)", "(SimpleChar:7953A9E6)", 12, 96056, 96024, 15),
			new CapturedSubwayCorpseEvidenceDefinition("20260709-220439", "2026-07-10T03:13:43.3390132Z", "(Corpse:00F6E002)", "(SimpleChar:7953AA32)", 15, 96056, 96024, 19),
			new CapturedSubwayCorpseEvidenceDefinition("20260709-225408", "2026-07-10T03:56:03.0128144Z", "(Corpse:00F6E005)", "(SimpleChar:79545319)", 12, 96056, 96024, 15),
			new CapturedSubwayCorpseEvidenceDefinition("20260709-225408", "2026-07-10T04:02:56.6662429Z", "(Corpse:00F6E023)", "(SimpleChar:795451AC)", 23, 96056, 96024, 29)
		}, new string[8] { "20260709-212115", "20260709-212336", "20260709-220439", "20260709-222339", "20260709-225408", "20260710-211430", "20260716-033326", "20260716-034104" }),
		new CapturedSubwayOrdinaryArchetypeDefinition("slum_runner", "slum_runner", "Slum Runner", 55648, 151, 0, 268980737, 0, 0, 31, 0, 1227u, 0, new CapturedSubwayTextureDefinition[5]
		{
			new CapturedSubwayTextureDefinition(0, 0, 0),
			new CapturedSubwayTextureDefinition(1, 0, 0),
			new CapturedSubwayTextureDefinition(2, 0, 0),
			new CapturedSubwayTextureDefinition(3, 0, 0),
			new CapturedSubwayTextureDefinition(4, 0, 0)
		}, new CapturedSubwayMeshDefinition[0], new CapturedSubwayCombatEvidenceDefinition(observed: true, runtimeReady: true, 5, 11, 4.210098, 0, 0, 1145196631, 94), new CapturedSubwayLootEvidenceDefinition[10]
		{
			new CapturedSubwayLootEvidenceDefinition(103002, 103003, 20, 1, 12, 833),
			new CapturedSubwayLootEvidenceDefinition(104218, 104219, 25, 1, 12, 833),
			new CapturedSubwayLootEvidenceDefinition(105305, 105306, 26, 1, 12, 833),
			new CapturedSubwayLootEvidenceDefinition(108942, 108943, 26, 1, 12, 833),
			new CapturedSubwayLootEvidenceDefinition(109386, 109387, 25, 1, 12, 833),
			new CapturedSubwayLootEvidenceDefinition(109450, 109451, 22, 1, 12, 833),
			new CapturedSubwayLootEvidenceDefinition(110438, 110439, 26, 1, 12, 833),
			new CapturedSubwayLootEvidenceDefinition(111337, 111338, 17, 1, 12, 833),
			new CapturedSubwayLootEvidenceDefinition(234875, 234875, 1, 1, 12, 833),
			new CapturedSubwayLootEvidenceDefinition(234876, 234876, 1, 2, 12, 1667)
		}, new CapturedSubwayLootOutcomeEvidenceDefinition[18]
		{
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-220439", "2026-07-10T03:13:59.199118Z", "(Corpse:00F6E002)", "(SimpleChar:7953AA19)", 55648, 12712, 0, 101513, 101514, 15),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-222339", "2026-07-10T03:25:05.630552Z", "(Corpse:00F6E00A)", "(SimpleChar:7954520E)", 55648, 1214, 0, 109938, 109939, 12),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-222339", "2026-07-10T03:25:07.496389Z", "(Corpse:00F6E01A)", "(SimpleChar:79545201)", 55648, 1248, 0, 103624, 103625, 14),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-225408", "2026-07-10T03:57:11.493998Z", "(Corpse:00F6E01E)", "(SimpleChar:7954532B)", 55648, 4953, 0, 109086, 109087, 15),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-225408", "2026-07-10T03:58:56.424559Z", "(Corpse:00F6E003)", "(SimpleChar:795451A2)", 55648, 6843, 0, 109301, 109302, 14),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-225408", "2026-07-10T03:58:57.273336Z", "(Corpse:00F6E00D)", "(SimpleChar:7954514E)", 55648, 6857, 0, 234877, 234877, 1),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-225408", "2026-07-10T03:58:57.273336Z", "(Corpse:00F6E00D)", "(SimpleChar:7954514E)", 55648, 6857, 1, 105737, 105738, 12),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260716-222201", "2026-07-17T03:23:45.148695Z", "(Corpse:00F69018)", "(SimpleChar:797024CD)", 55648, 1988, 0, 109386, 109387, 25),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260716-222201", "2026-07-17T03:23:47.738353Z", "(Corpse:00F69013)", "(SimpleChar:797024CC)", 55648, 2014, 0, 104218, 104219, 25),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260716-222201", "2026-07-17T03:23:48.608651Z", "(Corpse:00F69016)", "(SimpleChar:797024CF)", 55648, 2029, 0, 234875, 234875, 1),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260716-222201", "2026-07-17T03:23:48.608651Z", "(Corpse:00F69016)", "(SimpleChar:797024CF)", 55648, 2029, 1, 103002, 103003, 20),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260716-222201", "2026-07-17T03:23:50.438430Z", "(Corpse:00F6900E)", "(SimpleChar:797024C8)", 55648, 2048, 0, 110438, 110439, 26),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260716-222201", "2026-07-17T03:23:51.197809Z", "(Corpse:00F69019)", "(SimpleChar:797024D2)", 55648, 2058, 0, 109450, 109451, 22),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260716-222201", "2026-07-17T03:23:52.627672Z", "(Corpse:00F69020)", "(SimpleChar:797024D7)", 55648, 2070, 0, 234876, 234876, 1),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260716-222201", "2026-07-17T03:23:53.408198Z", "(Corpse:00F6901E)", "(SimpleChar:797024CA)", 55648, 2078, 0, 234876, 234876, 1),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260716-222201", "2026-07-17T03:23:53.408198Z", "(Corpse:00F6901E)", "(SimpleChar:797024CA)", 55648, 2078, 1, 105305, 105306, 26),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260716-222201", "2026-07-17T03:23:55.237649Z", "(Corpse:00F6901F)", "(SimpleChar:797024C7)", 55648, 2099, 0, 111337, 111338, 17),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260716-222201", "2026-07-17T03:23:58.047875Z", "(Corpse:00F69010)", "(SimpleChar:79702538)", 55648, 2126, 0, 108942, 108943, 26)
		}, new CapturedSubwayCorpseEvidenceDefinition[21]
		{
			new CapturedSubwayCorpseEvidenceDefinition("20260709-220439", "2026-07-10T03:07:20.0422231Z", "(Corpse:00F6E012)", "(SimpleChar:7953ABC0)", 12, 55648, 31774, 72),
			new CapturedSubwayCorpseEvidenceDefinition("20260709-220439", "2026-07-10T03:07:26.5251834Z", "(Corpse:00F6E013)", "(SimpleChar:7953ABC3)", 12, 55648, 31774, 72),
			new CapturedSubwayCorpseEvidenceDefinition("20260709-220439", "2026-07-10T03:10:11.6004561Z", "(Corpse:00F6E00A)", "(SimpleChar:7953A993)", 16, 55648, 31774, 98),
			new CapturedSubwayCorpseEvidenceDefinition("20260709-220439", "2026-07-10T03:13:57.0985852Z", "(Corpse:00F6E002)", "(SimpleChar:7953AA19)", 17, 55648, 31774, 105),
			new CapturedSubwayCorpseEvidenceDefinition("20260709-222339", "2026-07-10T03:24:58.3630868Z", "(Corpse:00F6E00A)", "(SimpleChar:7954520E)", 12, 55648, 31774, 72),
			new CapturedSubwayCorpseEvidenceDefinition("20260709-222339", "2026-07-10T03:25:04.4471882Z", "(Corpse:00F6E01A)", "(SimpleChar:79545201)", 11, 55648, 31774, 66),
			new CapturedSubwayCorpseEvidenceDefinition("20260709-225408", "2026-07-10T03:56:04.2623915Z", "(Corpse:00F6E00F)", "(SimpleChar:79545142)", 17, 55648, 31774, 105),
			new CapturedSubwayCorpseEvidenceDefinition("20260709-225408", "2026-07-10T03:57:09.6260378Z", "(Corpse:00F6E01E)", "(SimpleChar:7954532B)", 16, 55648, 31774, 98),
			new CapturedSubwayCorpseEvidenceDefinition("20260709-225408", "2026-07-10T03:58:36.2896328Z", "(Corpse:00F6E00B)", "(SimpleChar:7953AF7F)", 17, 55648, 31774, 105),
			new CapturedSubwayCorpseEvidenceDefinition("20260709-225408", "2026-07-10T03:58:41.2894522Z", "(Corpse:00F6E015)", "(SimpleChar:7953AF7B)", 16, 55648, 31774, 98),
			new CapturedSubwayCorpseEvidenceDefinition("20260709-225408", "2026-07-10T03:58:46.8628997Z", "(Corpse:00F6E003)", "(SimpleChar:795451A2)", 18, 55648, 31774, 111),
			new CapturedSubwayCorpseEvidenceDefinition("20260709-225408", "2026-07-10T03:58:48.5714137Z", "(Corpse:00F6E00D)", "(SimpleChar:7954514E)", 16, 55648, 31774, 98),
			new CapturedSubwayCorpseEvidenceDefinition("20260710-211430", "2026-07-11T02:16:12.2422923Z", "(Corpse:00F6C010)", "(SimpleChar:7957E62C)", 15, 55648, 31774, 92),
			new CapturedSubwayCorpseEvidenceDefinition("20260716-034656", "2026-07-16T08:47:13.2700424Z", "(Corpse:00F69005)", "(SimpleChar:796D4080)", 23, 55648, 31774, 144),
			new CapturedSubwayCorpseEvidenceDefinition("20260716-034656", "2026-07-16T08:47:18.6298994Z", "(Corpse:00F69007)", "(SimpleChar:796D407E)", 23, 55648, 31774, 144),
			new CapturedSubwayCorpseEvidenceDefinition("20260716-034656", "2026-07-16T08:47:26.3602996Z", "(Corpse:00F69008)", "(SimpleChar:796D4078)", 23, 55648, 31774, 144),
			new CapturedSubwayCorpseEvidenceDefinition("20260716-034656", "2026-07-16T08:47:30.0103766Z", "(Corpse:00F69009)", "(SimpleChar:796D4083)", 21, 55648, 31774, 131),
			new CapturedSubwayCorpseEvidenceDefinition("20260716-034656", "2026-07-16T08:47:33.0305642Z", "(Corpse:00F6900A)", "(SimpleChar:796D407A)", 22, 55648, 31774, 137),
			new CapturedSubwayCorpseEvidenceDefinition("20260716-034656", "2026-07-16T08:47:39.0605673Z", "(Corpse:00F6900B)", "(SimpleChar:796D407C)", 21, 55648, 31774, 131),
			new CapturedSubwayCorpseEvidenceDefinition("20260716-215947", "2026-07-17T03:01:06.4375298Z", "(Corpse:00F69002)", "(SimpleChar:797024AE)", 22, 55648, 31774, 137),
			new CapturedSubwayCorpseEvidenceDefinition("20260716-222201", "2026-07-17T03:22:53.6574578Z", "(Corpse:00F6901A)", "(SimpleChar:797024DA)", 20, 55648, 31774, 124)
		}, new string[11]
		{
			"20260709-212115", "20260709-212336", "20260709-220439", "20260709-222339", "20260709-225408", "20260710-211430", "20260716-033326", "20260716-034104", "20260716-034656", "20260716-215947",
			"20260716-222201"
		}),
		new CapturedSubwayOrdinaryArchetypeDefinition("looter", "looter", "Looter", 203745, 138, 0, 268964353, 0, 0, 31, 1, 1579u, 40695, new CapturedSubwayTextureDefinition[5]
		{
			new CapturedSubwayTextureDefinition(0, 0, 0),
			new CapturedSubwayTextureDefinition(1, 21824, 0),
			new CapturedSubwayTextureDefinition(2, 0, 0),
			new CapturedSubwayTextureDefinition(3, 21819, 0),
			new CapturedSubwayTextureDefinition(4, 21831, 0)
		}, new CapturedSubwayMeshDefinition[2]
		{
			new CapturedSubwayMeshDefinition(0, 40695u, 0, 4),
			new CapturedSubwayMeshDefinition(1, 7798u, 0, 2)
		}, new CapturedSubwayCombatEvidenceDefinition(observed: true, runtimeReady: true, 11, 11, 5.282358, 6, 0, 0, 15), new CapturedSubwayLootEvidenceDefinition[9]
		{
			new CapturedSubwayLootEvidenceDefinition(21605, 21605, 1, 1, 11, 909),
			new CapturedSubwayLootEvidenceDefinition(85501, 22343, 12, 1, 11, 909),
			new CapturedSubwayLootEvidenceDefinition(124422, 124422, 12, 1, 11, 909),
			new CapturedSubwayLootEvidenceDefinition(144082, 144083, 7, 1, 11, 909),
			new CapturedSubwayLootEvidenceDefinition(234874, 234874, 1, 1, 11, 909),
			new CapturedSubwayLootEvidenceDefinition(234875, 234875, 1, 1, 11, 909),
			new CapturedSubwayLootEvidenceDefinition(234877, 234877, 1, 1, 11, 909),
			new CapturedSubwayLootEvidenceDefinition(301713, 301713, 1, 1, 11, 909),
			new CapturedSubwayLootEvidenceDefinition(301714, 301714, 1, 1, 11, 909)
		}, new CapturedSubwayLootOutcomeEvidenceDefinition[9]
		{
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260708-143600", "2026-07-08T19:51:21.3332060Z", "(Corpse:00F6E009)", "(SimpleChar:794CD7D0)", 203745, 15285, 0, 234877, 234877, 1),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260708-143600", "2026-07-08T19:51:21.3332060Z", "(Corpse:00F6E009)", "(SimpleChar:794CD7D0)", 203745, 15285, 1, 144082, 144083, 7),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260708-143600", "2026-07-08T19:51:53.1634354Z", "(Corpse:00F6E00D)", "(SimpleChar:794CD7CD)", 203745, 15514, 0, 124422, 124422, 12),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260708-143600", "2026-07-08T19:52:54.2449692Z", "(Corpse:00F6E010)", "(SimpleChar:794DF0F7)", 203745, 15910, 0, 234874, 234874, 1),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260708-143600", "2026-07-08T19:52:54.2449692Z", "(Corpse:00F6E010)", "(SimpleChar:794DF0F7)", 203745, 15910, 1, 301714, 301714, 1),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-210452", "2026-07-10T02:10:40.526339Z", "(Corpse:00F6E002)", "(SimpleChar:7953AD7C)", 203745, 6388, 0, 234875, 234875, 1),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-210452", "2026-07-10T02:11:42.670561Z", "(Corpse:00F6E027)", "(SimpleChar:7953AD8D)", 203745, 6888, 0, 85501, 22343, 12),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-210452", "2026-07-10T02:12:13.138762Z", "(Corpse:00F6E019)", "(SimpleChar:79528FD6)", 203745, 7113, 0, 21605, 21605, 1),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-210452", "2026-07-10T02:12:13.138762Z", "(Corpse:00F6E019)", "(SimpleChar:79528FD6)", 203745, 7113, 1, 301713, 301713, 1)
		}, new CapturedSubwayCorpseEvidenceDefinition[11]
		{
			new CapturedSubwayCorpseEvidenceDefinition("20260708-143600", "2026-07-08T19:50:41.3303835Z", "(Corpse:00F6E004)", "(SimpleChar:794CD76C)", 10, 203745, 17870, 59),
			new CapturedSubwayCorpseEvidenceDefinition("20260708-143600", "2026-07-08T19:51:19.7109723Z", "(Corpse:00F6E009)", "(SimpleChar:794CD7D0)", 9, 203745, 17870, 53),
			new CapturedSubwayCorpseEvidenceDefinition("20260708-143600", "2026-07-08T19:51:50.0927831Z", "(Corpse:00F6E00D)", "(SimpleChar:794CD7CD)", 10, 203745, 17870, 59),
			new CapturedSubwayCorpseEvidenceDefinition("20260708-143600", "2026-07-08T19:52:25.7639346Z", "(Corpse:00F6E010)", "(SimpleChar:794DF0F7)", 10, 203745, 17870, 59),
			new CapturedSubwayCorpseEvidenceDefinition("20260708-143600", "2026-07-08T19:55:08.0324828Z", "(Corpse:00F6E00A)", "(SimpleChar:794CD772)", 10, 203745, 17870, 59),
			new CapturedSubwayCorpseEvidenceDefinition("20260708-143600", "2026-07-08T19:59:21.6859061Z", "(Corpse:00F6E00E)", "(SimpleChar:794CD776)", 10, 203745, 17870, 59),
			new CapturedSubwayCorpseEvidenceDefinition("20260709-210452", "2026-07-10T02:10:38.3409486Z", "(Corpse:00F6E002)", "(SimpleChar:7953AD7C)", 9, 203745, 17870, 53),
			new CapturedSubwayCorpseEvidenceDefinition("20260709-210452", "2026-07-10T02:11:39.2050672Z", "(Corpse:00F6E027)", "(SimpleChar:7953AD8D)", 10, 203745, 17870, 59),
			new CapturedSubwayCorpseEvidenceDefinition("20260709-210452", "2026-07-10T02:12:10.2717009Z", "(Corpse:00F6E019)", "(SimpleChar:79528FD6)", 10, 203745, 17870, 59),
			new CapturedSubwayCorpseEvidenceDefinition("20260709-210452", "2026-07-10T02:12:40.1364012Z", "(Corpse:00F6E004)", "(SimpleChar:79528F96)", 10, 203745, 17870, 59),
			new CapturedSubwayCorpseEvidenceDefinition("20260709-210452", "2026-07-10T02:14:04.2321943Z", "(Corpse:00F6E001)", "(SimpleChar:795312BC)", 10, 203745, 17870, 59)
		}, new string[5] { "20260708-143600", "20260709-210452", "20260709-212115", "20260709-212336", "20260710-202132" }, new CapturedSubwaySourceWeaponEvidenceDefinition[8]
		{
			new CapturedSubwaySourceWeaponEvidenceDefinition(2035487452, 123038, 123039, 12, "20260709-210452,20260709-212115,20260709-212336"),
			new CapturedSubwaySourceWeaponEvidenceDefinition(2035487691, 123038, 123039, 9, "20260709-210452,20260709-212115,20260709-212336"),
			new CapturedSubwaySourceWeaponEvidenceDefinition(2035568667, 123038, 123039, 8, "20260709-212115,20260709-212336"),
			new CapturedSubwaySourceWeaponEvidenceDefinition(2035568681, 123038, 123039, 9, "20260709-212115,20260709-212336"),
			new CapturedSubwaySourceWeaponEvidenceDefinition(2035568692, 123038, 123039, 12, "20260709-212115,20260709-212336"),
			new CapturedSubwaySourceWeaponEvidenceDefinition(2035568700, 123038, 123039, 11, "20260709-212115,20260709-212336"),
			new CapturedSubwaySourceWeaponEvidenceDefinition(2035645624, 123038, 123039, 8, "20260710-202132"),
			new CapturedSubwaySourceWeaponEvidenceDefinition(2035803597, 123038, 123039, 9, "20260710-202132")
		}),
		new CapturedSubwayOrdinaryArchetypeDefinition("infector", "infector", "Infector", 31909, 150, 0, 268964353, 0, 0, 31, 0, 1227u, 0, new CapturedSubwayTextureDefinition[5]
		{
			new CapturedSubwayTextureDefinition(0, 0, 0),
			new CapturedSubwayTextureDefinition(1, 0, 0),
			new CapturedSubwayTextureDefinition(2, 0, 0),
			new CapturedSubwayTextureDefinition(3, 0, 0),
			new CapturedSubwayTextureDefinition(4, 0, 0)
		}, new CapturedSubwayMeshDefinition[0], new CapturedSubwayCombatEvidenceDefinition(observed: true, runtimeReady: true, 16, 36, 5.016862, 0, 0, 1145919558, 35), new CapturedSubwayLootEvidenceDefinition[4]
		{
			new CapturedSubwayLootEvidenceDefinition(101507, 101508, 20, 1, 7, 1429),
			new CapturedSubwayLootEvidenceDefinition(101735, 101736, 21, 1, 7, 1429),
			new CapturedSubwayLootEvidenceDefinition(107491, 107492, 15, 1, 7, 1429),
			new CapturedSubwayLootEvidenceDefinition(234875, 234875, 1, 1, 7, 1429)
		}, new CapturedSubwayLootOutcomeEvidenceDefinition[4]
		{
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-222339", "2026-07-10T03:26:22.393422Z", "(Corpse:00F6E002)", "(SimpleChar:7954514F)", 31909, 2472, 0, 101735, 101736, 21),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-225408", "2026-07-10T04:04:34.372698Z", "(Corpse:00F6E011)", "(SimpleChar:795451C9)", 31909, 15119, 0, 101507, 101508, 20),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260710-211430", "2026-07-11T02:17:22.225137Z", "(Corpse:00F6C004)", "(SimpleChar:7957E658)", 31909, 4501, 0, 234875, 234875, 1),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260710-211430", "2026-07-11T02:17:22.225137Z", "(Corpse:00F6C004)", "(SimpleChar:7957E658)", 31909, 4501, 1, 107491, 107492, 15)
		}, new CapturedSubwayCorpseEvidenceDefinition[15]
		{
			new CapturedSubwayCorpseEvidenceDefinition("20260709-222339", "2026-07-10T03:26:18.6915380Z", "(Corpse:00F6E002)", "(SimpleChar:7954514F)", 17, 31909, 31868, 105),
			new CapturedSubwayCorpseEvidenceDefinition("20260709-222339", "2026-07-10T03:26:52.8902204Z", "(Corpse:00F6E015)", "(SimpleChar:79545150)", 16, 31909, 31868, 98),
			new CapturedSubwayCorpseEvidenceDefinition("20260709-222339", "2026-07-10T03:27:38.3547556Z", "(Corpse:00F6E017)", "(SimpleChar:79545153)", 17, 31909, 31868, 105),
			new CapturedSubwayCorpseEvidenceDefinition("20260709-222339", "2026-07-10T03:28:21.1703196Z", "(Corpse:00F6E00B)", "(SimpleChar:79545154)", 16, 31909, 31868, 98),
			new CapturedSubwayCorpseEvidenceDefinition("20260709-225408", "2026-07-10T04:00:09.1855999Z", "(Corpse:00F6E010)", "(SimpleChar:7953AD64)", 19, 31909, 31868, 118),
			new CapturedSubwayCorpseEvidenceDefinition("20260709-225408", "2026-07-10T04:00:17.9001847Z", "(Corpse:00F6E012)", "(SimpleChar:7954517D)", 19, 31909, 31868, 118),
			new CapturedSubwayCorpseEvidenceDefinition("20260709-225408", "2026-07-10T04:04:09.1808342Z", "(Corpse:00F6E011)", "(SimpleChar:795451C9)", 19, 31909, 31868, 118),
			new CapturedSubwayCorpseEvidenceDefinition("20260710-211430", "2026-07-11T02:16:47.3405240Z", "(Corpse:00F6C003)", "(SimpleChar:7957E648)", 18, 31909, 31868, 111),
			new CapturedSubwayCorpseEvidenceDefinition("20260712-223719", "2026-07-13T03:40:39.3862482Z", "(Corpse:00F6C006)", "(SimpleChar:7960787E)", 25, 31909, 31868, 156),
			new CapturedSubwayCorpseEvidenceDefinition("20260712-223719", "2026-07-13T03:41:51.1696455Z", "(Corpse:00F6C001)", "(SimpleChar:7960787F)", 25, 31909, 31868, 156),
			new CapturedSubwayCorpseEvidenceDefinition("20260712-232137", "2026-07-13T04:23:14.1623814Z", "(Corpse:00F6C005)", "(SimpleChar:79607AC5)", 24, 31909, 31868, 150),
			new CapturedSubwayCorpseEvidenceDefinition("20260712-232137", "2026-07-13T04:23:30.8922617Z", "(Corpse:00F6C008)", "(SimpleChar:79607AC6)", 24, 31909, 31868, 150),
			new CapturedSubwayCorpseEvidenceDefinition("20260712-232137", "2026-07-13T04:23:49.4425495Z", "(Corpse:00F6C00A)", "(SimpleChar:79607AD0)", 24, 31909, 31868, 150),
			new CapturedSubwayCorpseEvidenceDefinition("20260712-232137", "2026-07-13T04:24:05.9528869Z", "(Corpse:00F6C00B)", "(SimpleChar:79607AD2)", 24, 31909, 31868, 150),
			new CapturedSubwayCorpseEvidenceDefinition("20260712-232137", "2026-07-13T04:24:15.6827531Z", "(Corpse:00F6C00D)", "(SimpleChar:79607AD1)", 24, 31909, 31868, 150)
		}, new string[8] { "20260709-220439", "20260709-222339", "20260709-225408", "20260710-211430", "20260712-223719", "20260712-232137", "20260716-033326", "20260716-034104" }),
		new CapturedSubwayOrdinaryArchetypeDefinition("lost_thought", "lost_thought", "Lost Thought", 96193, 138, 0, 268964353, 0, 0, 31, 0, 1227u, 0, new CapturedSubwayTextureDefinition[5]
		{
			new CapturedSubwayTextureDefinition(0, 0, 0),
			new CapturedSubwayTextureDefinition(1, 0, 0),
			new CapturedSubwayTextureDefinition(2, 0, 0),
			new CapturedSubwayTextureDefinition(3, 0, 0),
			new CapturedSubwayTextureDefinition(4, 0, 0)
		}, new CapturedSubwayMeshDefinition[0], new CapturedSubwayCombatEvidenceDefinition(observed: false, runtimeReady: false, 0, 0, 0.0, 0, 0, 0, 0), new CapturedSubwayLootEvidenceDefinition[1]
		{
			new CapturedSubwayLootEvidenceDefinition(101675, 101676, 25, 1, 1, 10000)
		}, new CapturedSubwayLootOutcomeEvidenceDefinition[1]
		{
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-225408", "2026-07-10T04:02:45.868434Z", "(Corpse:00F6E002)", "(SimpleChar:795451C0)", 96193, 12282, 0, 101675, 101676, 25)
		}, new CapturedSubwayCorpseEvidenceDefinition[4]
		{
			new CapturedSubwayCorpseEvidenceDefinition("20260709-225408", "2026-07-10T03:58:52.1709158Z", "(Corpse:00F6E018)", "(SimpleChar:7953AECD)", 18, 96193, 96179, 23),
			new CapturedSubwayCorpseEvidenceDefinition("20260709-225408", "2026-07-10T03:58:54.0883494Z", "(Corpse:00F6E026)", "(SimpleChar:7953AED2)", 16, 96193, 96179, 20),
			new CapturedSubwayCorpseEvidenceDefinition("20260709-225408", "2026-07-10T04:02:33.8281797Z", "(Corpse:00F6E002)", "(SimpleChar:795451C0)", 21, 96193, 96179, 26),
			new CapturedSubwayCorpseEvidenceDefinition("20260709-225408", "2026-07-10T04:02:43.0439879Z", "(Corpse:00F6E022)", "(SimpleChar:795451B7)", 22, 96193, 96179, 28)
		}, new string[5] { "20260709-220439", "20260709-222339", "20260709-225408", "20260710-211430", "20260716-034104" }),
		new CapturedSubwayOrdinaryArchetypeDefinition("neural_burnout", "neural_burnout", "Neural Burnout", 203730, 148, 0, 268964353, 0, 0, 31, 0, 1899u, 29702, new CapturedSubwayTextureDefinition[5]
		{
			new CapturedSubwayTextureDefinition(0, 0, 0),
			new CapturedSubwayTextureDefinition(1, 9406, 0),
			new CapturedSubwayTextureDefinition(2, 9407, 0),
			new CapturedSubwayTextureDefinition(3, 9405, 0),
			new CapturedSubwayTextureDefinition(4, 9401, 0)
		}, new CapturedSubwayMeshDefinition[1]
		{
			new CapturedSubwayMeshDefinition(0, 29702u, 0, 4)
		}, new CapturedSubwayCombatEvidenceDefinition(observed: true, runtimeReady: true, 16, 22, 9.929338, 0, 0, 1397315377, 5), new CapturedSubwayLootEvidenceDefinition[3]
		{
			new CapturedSubwayLootEvidenceDefinition(26471, 26471, 14, 1, 4, 2500),
			new CapturedSubwayLootEvidenceDefinition(123021, 123021, 21, 1, 4, 2500),
			new CapturedSubwayLootEvidenceDefinition(124560, 124561, 16, 1, 4, 2500)
		}, new CapturedSubwayLootOutcomeEvidenceDefinition[3]
		{
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260710-211430", "2026-07-11T02:18:13.364532Z", "(Corpse:00F6C018)", "(SimpleChar:7957E5FA)", 203730, 5704, 0, 124560, 124561, 16),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260716-034104", "2026-07-16T08:42:23.838420Z", "(Corpse:00F69001)", "(SimpleChar:796CD74A)", 203730, 1899, 0, 26471, 26471, 14),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260716-034104", "2026-07-16T08:42:23.838420Z", "(Corpse:00F69001)", "(SimpleChar:796CD74A)", 203730, 1899, 1, 123021, 123021, 21)
		}, new CapturedSubwayCorpseEvidenceDefinition[7]
		{
			new CapturedSubwayCorpseEvidenceDefinition("20260709-222339", "2026-07-10T03:28:25.6386936Z", "(Corpse:00F6E017)", "(SimpleChar:7954524A)", 18, 203730, 5941, 111),
			new CapturedSubwayCorpseEvidenceDefinition("20260709-225408", "2026-07-10T03:58:32.2744624Z", "(Corpse:00F6E002)", "(SimpleChar:79545231)", 17, 203730, 5941, 105),
			new CapturedSubwayCorpseEvidenceDefinition("20260710-211430", "2026-07-11T02:17:32.5774768Z", "(Corpse:00F6C007)", "(SimpleChar:7957E656)", 16, 203730, 5941, 98),
			new CapturedSubwayCorpseEvidenceDefinition("20260712-223719", "2026-07-13T03:38:31.2303542Z", "(Corpse:00F6C009)", "(SimpleChar:79607873)", 23, 203730, 5941, 144),
			new CapturedSubwayCorpseEvidenceDefinition("20260716-034104", "2026-07-16T08:41:45.2084122Z", "(Corpse:00F69001)", "(SimpleChar:796CD74A)", 25, 203730, 5941, 156),
			new CapturedSubwayCorpseEvidenceDefinition("20260716-221358", "2026-07-17T03:15:05.3727786Z", "(Corpse:00F69007)", "(SimpleChar:79702517)", 25, 203730, 5941, 156),
			new CapturedSubwayCorpseEvidenceDefinition("20260716-221748", "2026-07-17T03:17:51.7135569Z", "(Corpse:00F69002)", "(SimpleChar:79702427)", 18, 203730, 5941, 111)
		}, new string[10] { "20260709-220439", "20260709-222339", "20260709-225408", "20260710-211430", "20260712-223719", "20260716-033326", "20260716-034104", "20260716-221358", "20260716-221748", "20260716-222201" }),
		new CapturedSubwayOrdinaryArchetypeDefinition("bloodcreeper", "bloodcreeper", "Bloodcreeper", 30379, 63, 0, 268980737, 0, 0, 31, 0, 1483u, 0, new CapturedSubwayTextureDefinition[5]
		{
			new CapturedSubwayTextureDefinition(0, 0, 0),
			new CapturedSubwayTextureDefinition(1, 0, 0),
			new CapturedSubwayTextureDefinition(2, 0, 0),
			new CapturedSubwayTextureDefinition(3, 0, 0),
			new CapturedSubwayTextureDefinition(4, 0, 0)
		}, new CapturedSubwayMeshDefinition[0], new CapturedSubwayCombatEvidenceDefinition(observed: true, runtimeReady: true, 21, 41, 4.999308, 1, 0, 1397446450, 16), new CapturedSubwayLootEvidenceDefinition[1]
		{
			new CapturedSubwayLootEvidenceDefinition(42640, 42641, 30, 1, 4, 2500)
		}, new CapturedSubwayLootOutcomeEvidenceDefinition[1]
		{
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260717-214751", "2026-07-18T02:49:22.2922948Z", "(Corpse:00F69012)", "(SimpleChar:7973F7AE)", 30379, 1265, 0, 42640, 42641, 30)
		}, new CapturedSubwayCorpseEvidenceDefinition[1]
		{
			new CapturedSubwayCorpseEvidenceDefinition("20260712-223719", "2026-07-13T03:38:15.9163351Z", "(Corpse:00F6C002)", "(SimpleChar:7960785D)", 24, 30379, 26978, 150)
		}, new string[6] { "20260709-222339", "20260709-225408", "20260712-223719", "20260716-033326", "20260716-034104", "20260717-214751" }),
		new CapturedSubwayOrdinaryArchetypeDefinition("deranged_shopper", "deranged_shopper", "Deranged Shopper", 203736, 138, 0, 268964353, 0, 0, 31, 0, 1835u, 40630, new CapturedSubwayTextureDefinition[5]
		{
			new CapturedSubwayTextureDefinition(0, 0, 0),
			new CapturedSubwayTextureDefinition(1, 30859, 0),
			new CapturedSubwayTextureDefinition(2, 30869, 0),
			new CapturedSubwayTextureDefinition(3, 40896, 0),
			new CapturedSubwayTextureDefinition(4, 15815, 0)
		}, new CapturedSubwayMeshDefinition[2]
		{
			new CapturedSubwayMeshDefinition(0, 40630u, 0, 4),
			new CapturedSubwayMeshDefinition(1, 95784u, 0, 2)
		}, new CapturedSubwayCombatEvidenceDefinition(observed: true, runtimeReady: true, 9, 9, 0.0, 6, 0, 0, 1), new CapturedSubwayLootEvidenceDefinition[2]
		{
			new CapturedSubwayLootEvidenceDefinition(123019, 123020, 6, 1, 2, 5000),
			new CapturedSubwayLootEvidenceDefinition(124465, 124466, 10, 1, 2, 5000)
		}, new CapturedSubwayLootOutcomeEvidenceDefinition[2]
		{
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260708-143600", "2026-07-08T19:49:42.3304212Z", "(Corpse:00F6E002)", "(SimpleChar:794DF0F5)", 203736, 14646, 0, 123019, 123020, 6),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-210452", "2026-07-10T02:11:23.4907664Z", "(Corpse:00F6E01D)", "(SimpleChar:7953AD91)", 203736, 7929, 0, 124465, 124466, 10)
		}, new CapturedSubwayCorpseEvidenceDefinition[2]
		{
			new CapturedSubwayCorpseEvidenceDefinition("20260708-143600", "2026-07-08T19:49:35.7577116Z", "(Corpse:00F6E002)", "(SimpleChar:794DF0F5)", 8, 203736, 5927, 47),
			new CapturedSubwayCorpseEvidenceDefinition("20260709-210452", "2026-07-10T02:11:16.4763853Z", "(Corpse:00F6E01D)", "(SimpleChar:7953AD91)", 9, 203736, 5927, 53)
		}, new string[3] { "20260708-143600", "20260709-210452", "20260710-202132" }, new CapturedSubwaySourceWeaponEvidenceDefinition[1]
		{
			new CapturedSubwaySourceWeaponEvidenceDefinition(2035762471, 125454, 125455, 8, "20260710-202132")
		}),
		new CapturedSubwayOrdinaryArchetypeDefinition("empty_shell", "empty_shell", "Empty Shell", 203731, 148, 0, 268964353, 0, 0, 31, 0, 1899u, 29696, new CapturedSubwayTextureDefinition[5]
		{
			new CapturedSubwayTextureDefinition(0, 9402, 0),
			new CapturedSubwayTextureDefinition(1, 9406, 0),
			new CapturedSubwayTextureDefinition(2, 9407, 0),
			new CapturedSubwayTextureDefinition(3, 9405, 0),
			new CapturedSubwayTextureDefinition(4, 9414, 0)
		}, new CapturedSubwayMeshDefinition[1]
		{
			new CapturedSubwayMeshDefinition(0, 29696u, 0, 4)
		}, new CapturedSubwayCombatEvidenceDefinition(observed: true, runtimeReady: false, 15, 15, 0.0, 0, 0, 1397315377, 1), new CapturedSubwayLootEvidenceDefinition[0], new CapturedSubwayLootOutcomeEvidenceDefinition[0], new CapturedSubwayCorpseEvidenceDefinition[2]
		{
			new CapturedSubwayCorpseEvidenceDefinition("20260709-225408", "2026-07-10T03:59:43.9712983Z", "(Corpse:00F6E008)", "(SimpleChar:7954519B)", 21, 203731, 5941, 131),
			new CapturedSubwayCorpseEvidenceDefinition("20260709-225408", "2026-07-10T03:59:50.7870978Z", "(Corpse:00F6E00F)", "(SimpleChar:79545179)", 19, 203731, 5941, 118)
		}, new string[4] { "20260709-222339", "20260709-225408", "20260716-033326", "20260716-034104" }),
		new CapturedSubwayOrdinaryArchetypeDefinition("fragmented_soul", "fragmented_soul", "Fragmented Soul", 203729, 148, 0, 268964353, 0, 0, 31, 0, 1643u, 29706, new CapturedSubwayTextureDefinition[5]
		{
			new CapturedSubwayTextureDefinition(0, 9402, 0),
			new CapturedSubwayTextureDefinition(1, 9404, 0),
			new CapturedSubwayTextureDefinition(2, 9407, 0),
			new CapturedSubwayTextureDefinition(3, 9405, 0),
			new CapturedSubwayTextureDefinition(4, 9401, 0)
		}, new CapturedSubwayMeshDefinition[2]
		{
			new CapturedSubwayMeshDefinition(0, 29706u, 0, 4),
			new CapturedSubwayMeshDefinition(1, 7834u, 0, 2)
		}, new CapturedSubwayCombatEvidenceDefinition(observed: true, runtimeReady: true, 18, 23, 0.0, 6, 0, 0, 2), new CapturedSubwayLootEvidenceDefinition[6]
		{
			new CapturedSubwayLootEvidenceDefinition(26471, 26471, 14, 3, 4, 7500),
			new CapturedSubwayLootEvidenceDefinition(85691, 22004, 18, 1, 4, 2500),
			new CapturedSubwayLootEvidenceDefinition(85732, 21963, 17, 1, 4, 2500),
			new CapturedSubwayLootEvidenceDefinition(124304, 124305, 17, 1, 4, 2500),
			new CapturedSubwayLootEvidenceDefinition(234877, 234877, 1, 2, 4, 5000),
			new CapturedSubwayLootEvidenceDefinition(301712, 301712, 1, 1, 4, 2500)
		}, new CapturedSubwayLootOutcomeEvidenceDefinition[9]
		{
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-225408", "2026-07-10T03:59:48.5697867Z", "(Corpse:00F6E001)", "(SimpleChar:7954517A)", 203729, 8749, 0, 26471, 26471, 14),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-225408", "2026-07-10T04:00:02.8691421Z", "(Corpse:00F6E024)", "(SimpleChar:7954518A)", 203729, 9224, 1, 234877, 234877, 1),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-225408", "2026-07-10T04:00:02.8691421Z", "(Corpse:00F6E024)", "(SimpleChar:7954518A)", 203729, 9224, 2, 85732, 21963, 17),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-225408", "2026-07-10T04:01:29.265990Z", "(Corpse:00F6E006)", "(SimpleChar:795451AE)", 203729, 11059, 0, 26471, 26471, 14),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-225408", "2026-07-10T04:01:29.265990Z", "(Corpse:00F6E006)", "(SimpleChar:795451AE)", 203729, 11059, 1, 234877, 234877, 1),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-225408", "2026-07-10T04:01:29.265990Z", "(Corpse:00F6E006)", "(SimpleChar:795451AE)", 203729, 11059, 2, 124304, 124305, 17),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-225408", "2026-07-10T04:01:58.363857Z", "(Corpse:00F6E003)", "(SimpleChar:795451AA)", 203729, 11589, 0, 26471, 26471, 14),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-225408", "2026-07-10T04:01:58.363857Z", "(Corpse:00F6E003)", "(SimpleChar:795451AA)", 203729, 11589, 1, 85691, 22004, 18),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-225408", "2026-07-10T04:01:58.363857Z", "(Corpse:00F6E003)", "(SimpleChar:795451AA)", 203729, 11589, 2, 301712, 301712, 1)
		}, new CapturedSubwayCorpseEvidenceDefinition[5]
		{
			new CapturedSubwayCorpseEvidenceDefinition("20260709-222339", "2026-07-10T03:27:20.3410663Z", "(Corpse:00F6E009)", "(SimpleChar:79545248)", 18, 203729, 5921, 111),
			new CapturedSubwayCorpseEvidenceDefinition("20260709-225408", "2026-07-10T04:01:25.8684599Z", "(Corpse:00F6E006)", "(SimpleChar:795451AE)", 21, 203729, 5921, 131),
			new CapturedSubwayCorpseEvidenceDefinition("20260709-225408", "2026-07-10T04:01:56.1484716Z", "(Corpse:00F6E003)", "(SimpleChar:795451AA)", 21, 203729, 5921, 131),
			new CapturedSubwayCorpseEvidenceDefinition("20260712-224608", "2026-07-13T03:46:40.0868425Z", "(Corpse:00F6C002)", "(SimpleChar:796079C4)", 18, 203729, 5921, 111),
			new CapturedSubwayCorpseEvidenceDefinition("20260716-222007", "2026-07-17T03:20:28.1376938Z", "(Corpse:00F6901F)", "(SimpleChar:7970245D)", 17, 203729, 5921, 105)
		}, new string[7] { "20260709-222339", "20260709-225408", "20260710-211430", "20260712-224608", "20260716-033326", "20260716-034104", "20260716-222007" }),
		new CapturedSubwayOrdinaryArchetypeDefinition("incomplete_rebuild", "incomplete_rebuild", "Incomplete Rebuild", 203728, 148, 0, 268964353, 0, 0, 31, 0, 1643u, 29694, new CapturedSubwayTextureDefinition[5]
		{
			new CapturedSubwayTextureDefinition(0, 9402, 0),
			new CapturedSubwayTextureDefinition(1, 9404, 0),
			new CapturedSubwayTextureDefinition(2, 9407, 0),
			new CapturedSubwayTextureDefinition(3, 9405, 0),
			new CapturedSubwayTextureDefinition(4, 9401, 0)
		}, new CapturedSubwayMeshDefinition[2]
		{
			new CapturedSubwayMeshDefinition(0, 29694u, 0, 4),
			new CapturedSubwayMeshDefinition(1, 7830u, 0, 2)
		}, new CapturedSubwayCombatEvidenceDefinition(observed: true, runtimeReady: true, 17, 35, 0.0, 6, 0, 0, 2), new CapturedSubwayLootEvidenceDefinition[2]
		{
			new CapturedSubwayLootEvidenceDefinition(26503, 26503, 14, 1, 2, 5000),
			new CapturedSubwayLootEvidenceDefinition(142817, 142818, 16, 1, 2, 5000)
		}, new CapturedSubwayLootOutcomeEvidenceDefinition[2]
		{
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-225408", "2026-07-10T04:00:01.0182829Z", "(Corpse:00F6E014)", "(SimpleChar:79545188)", 203728, 9178, 0, 26503, 26503, 14),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260710-211430", "2026-07-11T02:18:20.754360Z", "(Corpse:00F6C014)", "(SimpleChar:7957E5F9)", 203728, 5807, 0, 142817, 142818, 16)
		}, new CapturedSubwayCorpseEvidenceDefinition[7]
		{
			new CapturedSubwayCorpseEvidenceDefinition("20260709-222339", "2026-07-10T03:25:49.9443021Z", "(Corpse:00F6E00F)", "(SimpleChar:79545241)", 17, 203728, 5921, 105),
			new CapturedSubwayCorpseEvidenceDefinition("20260709-225408", "2026-07-10T04:00:49.2520950Z", "(Corpse:00F6E012)", "(SimpleChar:795451CB)", 21, 203728, 5921, 131),
			new CapturedSubwayCorpseEvidenceDefinition("20260709-225408", "2026-07-10T04:01:02.6007301Z", "(Corpse:00F6E01B)", "(SimpleChar:795451FD)", 19, 203728, 5921, 118),
			new CapturedSubwayCorpseEvidenceDefinition("20260709-225408", "2026-07-10T04:02:19.1138049Z", "(Corpse:00F6E015)", "(SimpleChar:795451C1)", 19, 203728, 5921, 118),
			new CapturedSubwayCorpseEvidenceDefinition("20260709-225408", "2026-07-10T04:02:37.4294298Z", "(Corpse:00F6E00E)", "(SimpleChar:795451BC)", 21, 203728, 5921, 131),
			new CapturedSubwayCorpseEvidenceDefinition("20260716-222007", "2026-07-17T03:20:16.0778439Z", "(Corpse:00F69009)", "(SimpleChar:79702438)", 18, 203728, 5921, 111),
			new CapturedSubwayCorpseEvidenceDefinition("20260716-222007", "2026-07-17T03:20:24.6178720Z", "(Corpse:00F69012)", "(SimpleChar:79702459)", 19, 203728, 5921, 118)
		}, new string[7] { "20260709-222339", "20260709-225408", "20260710-211430", "20260716-033326", "20260716-034104", "20260716-222007", "20260716-222201" }, new CapturedSubwaySourceWeaponEvidenceDefinition[10]
		{
			new CapturedSubwaySourceWeaponEvidenceDefinition(2035569008, 122653, 122654, 18, "20260709-222339"),
			new CapturedSubwaySourceWeaponEvidenceDefinition(2035569010, 122653, 122654, 14, "20260709-222339"),
			new CapturedSubwaySourceWeaponEvidenceDefinition(2035569015, 122653, 122654, 18, "20260709-222339"),
			new CapturedSubwaySourceWeaponEvidenceDefinition(2035569025, 122654, 122654, 20, "20260709-222339"),
			new CapturedSubwaySourceWeaponEvidenceDefinition(2035569032, 122653, 122654, 17, "20260709-222339"),
			new CapturedSubwaySourceWeaponEvidenceDefinition(2035569084, 122653, 122654, 18, "20260709-222339,20260709-225408"),
			new CapturedSubwaySourceWeaponEvidenceDefinition(2035569089, 122655, 122655, 21, "20260709-222339,20260709-225408"),
			new CapturedSubwaySourceWeaponEvidenceDefinition(2035569099, 122655, 122656, 24, "20260709-222339,20260709-225408"),
			new CapturedSubwaySourceWeaponEvidenceDefinition(2035569149, 122654, 122654, 20, "20260709-222339,20260709-225408"),
			new CapturedSubwaySourceWeaponEvidenceDefinition(2035569217, 122654, 122654, 20, "20260709-222339")
		}),
		new CapturedSubwayOrdinaryArchetypeDefinition("melded_patterns", "melded_patterns", "Melded Patterns", 203747, 148, 0, 268964353, 0, 0, 31, 0, 1899u, 29701, new CapturedSubwayTextureDefinition[5]
		{
			new CapturedSubwayTextureDefinition(0, 0, 0),
			new CapturedSubwayTextureDefinition(1, 9406, 0),
			new CapturedSubwayTextureDefinition(2, 9407, 0),
			new CapturedSubwayTextureDefinition(3, 9405, 0),
			new CapturedSubwayTextureDefinition(4, 9401, 0)
		}, new CapturedSubwayMeshDefinition[2]
		{
			new CapturedSubwayMeshDefinition(0, 29701u, 0, 4),
			new CapturedSubwayMeshDefinition(1, 7789u, 0, 2)
		}, new CapturedSubwayCombatEvidenceDefinition(observed: true, runtimeReady: true, 21, 34, 4.466488, 6, 0, 0, 7), new CapturedSubwayLootEvidenceDefinition[5]
		{
			new CapturedSubwayLootEvidenceDefinition(122672, 122673, 15, 1, 4, 2500),
			new CapturedSubwayLootEvidenceDefinition(144067, 144068, 23, 1, 4, 2500),
			new CapturedSubwayLootEvidenceDefinition(152328, 152329, 24, 1, 4, 2500),
			new CapturedSubwayLootEvidenceDefinition(234874, 234874, 1, 1, 4, 2500),
			new CapturedSubwayLootEvidenceDefinition(301710, 301710, 1, 1, 4, 2500)
		}, new CapturedSubwayLootOutcomeEvidenceDefinition[5]
		{
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-225408", "2026-07-10T04:00:32.935382Z", "(Corpse:00F6E013)", "(SimpleChar:79545190)", 203747, 9402, 0, 234874, 234874, 1),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-225408", "2026-07-10T04:00:32.935382Z", "(Corpse:00F6E013)", "(SimpleChar:79545190)", 203747, 9402, 1, 122672, 122673, 15),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-225408", "2026-07-10T04:00:52.934134Z", "(Corpse:00F6E001)", "(SimpleChar:79545196)", 203747, 10152, 0, 152328, 152329, 24),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260712-223719", "2026-07-13T03:39:40.2441054Z", "(Corpse:00F6C014)", "(SimpleChar:79607872)", 203747, 2997, 0, 144067, 144068, 23),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260712-223719", "2026-07-13T03:39:40.2441054Z", "(Corpse:00F6C014)", "(SimpleChar:79607872)", 203747, 2997, 1, 301710, 301710, 1)
		}, new CapturedSubwayCorpseEvidenceDefinition[10]
		{
			new CapturedSubwayCorpseEvidenceDefinition("20260709-225408", "2026-07-10T04:00:05.7178352Z", "(Corpse:00F6E001)", "(SimpleChar:7954517C)", 18, 203747, 23368, 111),
			new CapturedSubwayCorpseEvidenceDefinition("20260709-225408", "2026-07-10T04:00:11.9526652Z", "(Corpse:00F6E011)", "(SimpleChar:79545187)", 21, 203747, 23368, 131),
			new CapturedSubwayCorpseEvidenceDefinition("20260709-225408", "2026-07-10T04:00:25.3985330Z", "(Corpse:00F6E013)", "(SimpleChar:79545190)", 18, 203747, 23368, 111),
			new CapturedSubwayCorpseEvidenceDefinition("20260709-225408", "2026-07-10T04:00:47.0436246Z", "(Corpse:00F6E001)", "(SimpleChar:79545196)", 20, 203747, 23368, 124),
			new CapturedSubwayCorpseEvidenceDefinition("20260709-225408", "2026-07-10T04:00:50.5009687Z", "(Corpse:00F6E019)", "(SimpleChar:79545198)", 21, 203747, 23368, 131),
			new CapturedSubwayCorpseEvidenceDefinition("20260709-225408", "2026-07-10T04:03:58.8677826Z", "(Corpse:00F6E010)", "(SimpleChar:795451D8)", 25, 203747, 23368, 156),
			new CapturedSubwayCorpseEvidenceDefinition("20260709-225408", "2026-07-10T04:04:33.8903813Z", "(Corpse:00F6E009)", "(SimpleChar:795451DD)", 25, 203747, 23368, 156),
			new CapturedSubwayCorpseEvidenceDefinition("20260712-223719", "2026-07-13T03:39:17.3969861Z", "(Corpse:00F6C014)", "(SimpleChar:79607872)", 25, 203747, 23368, 156),
			new CapturedSubwayCorpseEvidenceDefinition("20260712-223719", "2026-07-13T03:39:21.8903988Z", "(Corpse:00F6C006)", "(SimpleChar:79607878)", 24, 203747, 23368, 150),
			new CapturedSubwayCorpseEvidenceDefinition("20260716-215947", "2026-07-17T03:01:29.8595413Z", "(Corpse:00F69005)", "(SimpleChar:79702235)", 21, 203747, 23368, 131)
		}, new string[9] { "20260709-222339", "20260709-225408", "20260710-211430", "20260712-223719", "20260716-033326", "20260716-034104", "20260716-034559", "20260716-215947", "20260716-222201" }),
		new CapturedSubwayOrdinaryArchetypeDefinition("molested_molecules", "molested_molecules", "Molested Molecules", 203746, 148, 0, 268964353, 0, 0, 31, 0, 1643u, 29704, new CapturedSubwayTextureDefinition[5]
		{
			new CapturedSubwayTextureDefinition(0, 9402, 0),
			new CapturedSubwayTextureDefinition(1, 9404, 0),
			new CapturedSubwayTextureDefinition(2, 9407, 0),
			new CapturedSubwayTextureDefinition(3, 9405, 0),
			new CapturedSubwayTextureDefinition(4, 9401, 0)
		}, new CapturedSubwayMeshDefinition[2]
		{
			new CapturedSubwayMeshDefinition(0, 29704u, 0, 4),
			new CapturedSubwayMeshDefinition(1, 35547u, 0, 2)
		}, new CapturedSubwayCombatEvidenceDefinition(observed: true, runtimeReady: true, 16, 42, 4.749995, 6, 0, 0, 20), new CapturedSubwayLootEvidenceDefinition[4]
		{
			new CapturedSubwayLootEvidenceDefinition(27199, 27199, 10, 1, 3, 3333),
			new CapturedSubwayLootEvidenceDefinition(121743, 121744, 25, 1, 3, 3333),
			new CapturedSubwayLootEvidenceDefinition(301712, 301712, 1, 1, 3, 3333),
			new CapturedSubwayLootEvidenceDefinition(301713, 301713, 1, 1, 3, 3333)
		}, new CapturedSubwayLootOutcomeEvidenceDefinition[9]
		{
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-225408", "2026-07-10T04:02:11.747683Z", "(Corpse:00F6E00D)", "(SimpleChar:795451B5)", 203746, 11744, 0, 26471, 26471, 14),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-225408", "2026-07-10T04:04:12.350937Z", "(Corpse:00F6E00F)", "(SimpleChar:795451C2)", 203746, 14512, 0, 234877, 234877, 1),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-225408", "2026-07-10T04:04:12.350937Z", "(Corpse:00F6E00F)", "(SimpleChar:795451C2)", 203746, 14512, 1, 27263, 27263, 10),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-225408", "2026-07-10T04:04:12.350937Z", "(Corpse:00F6E00F)", "(SimpleChar:795451C2)", 203746, 14512, 2, 122028, 122029, 25),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-225408", "2026-07-10T04:04:38.606100Z", "(Corpse:00F6E006)", "(SimpleChar:795450E5)", 203746, 15201, 0, 26471, 26471, 14),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260716-034104", "2026-07-16T08:42:42.029316Z", "(Corpse:00F69003)", "(SimpleChar:796CD747)", 203746, 2398, 0, 27199, 27199, 10),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260716-034104", "2026-07-16T08:42:42.029316Z", "(Corpse:00F69003)", "(SimpleChar:796CD747)", 203746, 2398, 1, 121743, 121744, 25),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260716-034104", "2026-07-16T08:42:42.029316Z", "(Corpse:00F69003)", "(SimpleChar:796CD747)", 203746, 2398, 2, 301712, 301712, 1),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260716-221358", "2026-07-17T03:14:34.469096Z", "(Corpse:00F69020)", "(SimpleChar:79702515)", 203746, 776, 0, 301713, 301713, 1)
		}, new CapturedSubwayCorpseEvidenceDefinition[8]
		{
			new CapturedSubwayCorpseEvidenceDefinition("20260709-225408", "2026-07-10T04:02:03.3982727Z", "(Corpse:00F6E00D)", "(SimpleChar:795451B5)", 20, 203746, 5921, 124),
			new CapturedSubwayCorpseEvidenceDefinition("20260709-225408", "2026-07-10T04:04:01.6934488Z", "(Corpse:00F6E00F)", "(SimpleChar:795451C2)", 21, 203746, 5921, 131),
			new CapturedSubwayCorpseEvidenceDefinition("20260709-225408", "2026-07-10T04:04:30.5605886Z", "(Corpse:00F6E006)", "(SimpleChar:795450E5)", 20, 203746, 5921, 124),
			new CapturedSubwayCorpseEvidenceDefinition("20260712-223719", "2026-07-13T03:37:53.5689640Z", "(Corpse:00F6C003)", "(SimpleChar:795F951A)", 22, 203746, 5921, 137),
			new CapturedSubwayCorpseEvidenceDefinition("20260712-223719", "2026-07-13T03:38:36.1067870Z", "(Corpse:00F6C00E)", "(SimpleChar:79607874)", 24, 203746, 5921, 150),
			new CapturedSubwayCorpseEvidenceDefinition("20260716-215947", "2026-07-17T03:01:25.9795236Z", "(Corpse:00F69004)", "(SimpleChar:7970223C)", 23, 203746, 5921, 144),
			new CapturedSubwayCorpseEvidenceDefinition("20260716-221358", "2026-07-17T03:14:44.2626971Z", "(Corpse:00F69020)", "(SimpleChar:7970251A)", 25, 203746, 5921, 156),
			new CapturedSubwayCorpseEvidenceDefinition("20260716-222007", "2026-07-17T03:20:30.4568500Z", "(Corpse:00F69020)", "(SimpleChar:7970245E)", 19, 203746, 5921, 118)
		}, new string[10] { "20260709-222339", "20260709-225408", "20260710-211430", "20260712-223719", "20260716-033326", "20260716-034104", "20260716-215947", "20260716-221358", "20260716-222007", "20260716-222201" }),
		new CapturedSubwayOrdinaryArchetypeDefinition("premature_pattern", "premature_pattern", "Premature Pattern", 203727, 148, 0, 268964353, 0, 0, 31, 0, 1899u, 29699, new CapturedSubwayTextureDefinition[5]
		{
			new CapturedSubwayTextureDefinition(0, 9402, 0),
			new CapturedSubwayTextureDefinition(1, 9406, 0),
			new CapturedSubwayTextureDefinition(2, 9407, 0),
			new CapturedSubwayTextureDefinition(3, 9405, 0),
			new CapturedSubwayTextureDefinition(4, 9401, 0)
		}, new CapturedSubwayMeshDefinition[1]
		{
			new CapturedSubwayMeshDefinition(0, 29699u, 0, 4)
		}, new CapturedSubwayCombatEvidenceDefinition(observed: true, runtimeReady: false, 22, 22, 0.0, 0, 0, 1397315377, 1), new CapturedSubwayLootEvidenceDefinition[0], new CapturedSubwayLootOutcomeEvidenceDefinition[0], new CapturedSubwayCorpseEvidenceDefinition[4]
		{
			new CapturedSubwayCorpseEvidenceDefinition("20260710-211430", "2026-07-11T02:18:16.4225480Z", "(Corpse:00F6C019)", "(SimpleChar:7957E65A)", 17, 203727, 5941, 105),
			new CapturedSubwayCorpseEvidenceDefinition("20260712-223719", "2026-07-13T03:37:53.5679637Z", "(Corpse:00F6C001)", "(SimpleChar:795F9516)", 23, 203727, 5941, 144),
			new CapturedSubwayCorpseEvidenceDefinition("20260712-224608", "2026-07-13T03:47:23.8937503Z", "(Corpse:00F6C004)", "(SimpleChar:796079C3)", 18, 203727, 5941, 111),
			new CapturedSubwayCorpseEvidenceDefinition("20260716-215947", "2026-07-17T03:01:36.7896724Z", "(Corpse:00F69009)", "(SimpleChar:79702236)", 23, 203727, 5941, 144)
		}, new string[9] { "20260709-220439", "20260709-222339", "20260709-225408", "20260710-211430", "20260712-223719", "20260712-224608", "20260716-033326", "20260716-034104", "20260716-215947" }),
		new CapturedSubwayOrdinaryArchetypeDefinition("redundant_scan", "redundant_scan", "Redundant Scan", 204178, 148, 0, 268964353, 0, 0, 31, 0, 1899u, 40660, new CapturedSubwayTextureDefinition[5]
		{
			new CapturedSubwayTextureDefinition(0, 9402, 0),
			new CapturedSubwayTextureDefinition(1, 9406, 0),
			new CapturedSubwayTextureDefinition(2, 9407, 0),
			new CapturedSubwayTextureDefinition(3, 0, 0),
			new CapturedSubwayTextureDefinition(4, 9414, 0)
		}, new CapturedSubwayMeshDefinition[2]
		{
			new CapturedSubwayMeshDefinition(0, 40660u, 0, 4),
			new CapturedSubwayMeshDefinition(1, 7817u, 0, 2)
		}, new CapturedSubwayCombatEvidenceDefinition(observed: true, runtimeReady: true, 19, 19, 0.0, 6, 0, 0, 1), new CapturedSubwayLootEvidenceDefinition[1]
		{
			new CapturedSubwayLootEvidenceDefinition(27263, 27263, 10, 1, 2, 5000)
		}, new CapturedSubwayLootOutcomeEvidenceDefinition[1]
		{
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-225408", "2026-07-10T04:02:05.448100Z", "(Corpse:00F6E00B)", "(SimpleChar:795451BF)", 204178, 11693, 0, 27263, 27263, 10)
		}, new CapturedSubwayCorpseEvidenceDefinition[4]
		{
			new CapturedSubwayCorpseEvidenceDefinition("20260709-225408", "2026-07-10T04:00:59.2504528Z", "(Corpse:00F6E01A)", "(SimpleChar:795451C4)", 21, 204178, 23370, 131),
			new CapturedSubwayCorpseEvidenceDefinition("20260709-225408", "2026-07-10T04:01:49.0795554Z", "(Corpse:00F6E00B)", "(SimpleChar:795451BF)", 19, 204178, 23370, 118),
			new CapturedSubwayCorpseEvidenceDefinition("20260709-225408", "2026-07-10T04:04:27.0939744Z", "(Corpse:00F6E013)", "(SimpleChar:7953AF85)", 20, 204178, 23370, 124),
			new CapturedSubwayCorpseEvidenceDefinition("20260716-222201", "2026-07-17T03:22:03.1494706Z", "(Corpse:00F69009)", "(SimpleChar:7970250F)", 22, 204178, 23370, 137)
		}, new string[5] { "20260709-222339", "20260709-225408", "20260716-033326", "20260716-034104", "20260716-222201" }, new CapturedSubwaySourceWeaponEvidenceDefinition[4]
		{
			new CapturedSubwaySourceWeaponEvidenceDefinition(2035527557, 122027, 122027, 20, "20260709-222339,20260709-225408"),
			new CapturedSubwaySourceWeaponEvidenceDefinition(2035569087, 122026, 122027, 14, "20260709-222339,20260709-225408"),
			new CapturedSubwaySourceWeaponEvidenceDefinition(2035569092, 122028, 122029, 25, "20260709-222339,20260709-225408"),
			new CapturedSubwaySourceWeaponEvidenceDefinition(2035569107, 122026, 122027, 16, "20260709-222339,20260709-225408")
		}),
		new CapturedSubwayOrdinaryArchetypeDefinition("uncontrollable_anger", "uncontrollable_anger", "Uncontrollable Anger", 96195, 138, 0, 268964353, 0, 0, 31, 0, 1227u, 0, new CapturedSubwayTextureDefinition[5]
		{
			new CapturedSubwayTextureDefinition(0, 0, 0),
			new CapturedSubwayTextureDefinition(1, 0, 0),
			new CapturedSubwayTextureDefinition(2, 0, 0),
			new CapturedSubwayTextureDefinition(3, 0, 0),
			new CapturedSubwayTextureDefinition(4, 0, 0)
		}, new CapturedSubwayMeshDefinition[0], new CapturedSubwayCombatEvidenceDefinition(observed: true, runtimeReady: true, 11, 18, 5.167153, 0, 0, 1397315377, 2), new CapturedSubwayLootEvidenceDefinition[3]
		{
			new CapturedSubwayLootEvidenceDefinition(101809, 101810, 24, 1, 2, 5000),
			new CapturedSubwayLootEvidenceDefinition(109366, 109367, 9, 1, 2, 5000),
			new CapturedSubwayLootEvidenceDefinition(290619, 202727, 19, 1, 2, 5000)
		}, new CapturedSubwayLootOutcomeEvidenceDefinition[3]
		{
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260709-225408", "2026-07-10T03:55:41.763030Z", "(Corpse:00F6E003)", "(SimpleChar:79545314)", 96195, 2842, 0, 109366, 109367, 9),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260710-211430", "2026-07-11T02:17:25.556829Z", "(Corpse:00F6C005)", "(SimpleChar:7957E653)", 96195, 4555, 0, 290619, 202727, 19),
			new CapturedSubwayLootOutcomeEvidenceDefinition("20260710-211430", "2026-07-11T02:17:25.556829Z", "(Corpse:00F6C005)", "(SimpleChar:7957E653)", 96195, 4555, 1, 101809, 101810, 24)
		}, new CapturedSubwayCorpseEvidenceDefinition[6]
		{
			new CapturedSubwayCorpseEvidenceDefinition("20260709-220439", "2026-07-10T03:07:16.7584997Z", "(Corpse:00F6E011)", "(SimpleChar:7953AD65)", 13, 96195, 96177, 16),
			new CapturedSubwayCorpseEvidenceDefinition("20260709-222339", "2026-07-10T03:24:59.5786414Z", "(Corpse:00F6E00B)", "(SimpleChar:79545212)", 13, 96195, 96177, 16),
			new CapturedSubwayCorpseEvidenceDefinition("20260709-225408", "2026-07-10T03:55:39.0892723Z", "(Corpse:00F6E003)", "(SimpleChar:79545314)", 11, 96195, 96177, 14),
			new CapturedSubwayCorpseEvidenceDefinition("20260709-225408", "2026-07-10T04:02:23.0450443Z", "(Corpse:00F6E01F)", "(SimpleChar:795451B9)", 20, 96195, 96177, 25),
			new CapturedSubwayCorpseEvidenceDefinition("20260710-211430", "2026-07-11T02:15:55.1134494Z", "(Corpse:00F6C00E)", "(SimpleChar:7957E630)", 12, 96195, 96177, 15),
			new CapturedSubwayCorpseEvidenceDefinition("20260710-211430", "2026-07-11T02:17:20.9725328Z", "(Corpse:00F6C005)", "(SimpleChar:7957E653)", 21, 96195, 96177, 26)
		}, new string[10] { "20260709-205921", "20260709-210452", "20260709-212115", "20260709-212336", "20260709-220439", "20260709-222339", "20260709-225408", "20260710-211430", "20260716-033326", "20260716-034104" })
	};

	private static readonly CapturedSubwayOrdinarySpawnDefinition[] Spawns = new CapturedSubwayOrdinarySpawnDefinition[197]
	{
		new CapturedSubwayOrdinarySpawnDefinition(2035526067, "architect_striker", 15, 393, 0, 97, 52, 334.5223f, 102.4164f, 245.14511f, 0f, -0.5343819f, 0f, 0.84524316f, (SimpleCharFullUpdateFlags)36391627, 0, "80000000000000000000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[1]
		{
			new CapturedSubwayWaypointDefinition(334.5223f, 102.4164f, 245.14511f)
		}, "", "20260709-212336", "2026-07-10T02:26:37.6139282Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035526070, "architect_striker", 13, 327, 0, 96, 45, 337.79706f, 102.4164f, 245.4825f, 0f, 0.5905517f, 0f, 0.8069998f, (SimpleCharFullUpdateFlags)36391627, 0, "00000000000000000000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[1]
		{
			new CapturedSubwayWaypointDefinition(337.79706f, 102.4164f, 245.4825f)
		}, "", "20260709-212336", "2026-07-10T02:26:37.6139282Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035526077, "architect_striker", 13, 327, 0, 96, 45, 306.06085f, 102.8164f, 252.91737f, 0f, -0.7477698f, 0f, 0.66395813f, (SimpleCharFullUpdateFlags)36391627, 0, "80000000000000008000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[1]
		{
			new CapturedSubwayWaypointDefinition(306.06085f, 102.8164f, 252.91737f)
		}, "", "20260709-212336", "2026-07-10T02:26:37.6139282Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035526379, "architect_striker", 14, 360, 0, 97, 48, 335.20175f, 102.4164f, 250.61977f, 0f, -0.6084441f, 0f, 0.79359674f, (SimpleCharFullUpdateFlags)36391627, 0, "80000000000000000000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[1]
		{
			new CapturedSubwayWaypointDefinition(335.20175f, 102.4164f, 250.61977f)
		}, "", "20260709-212336", "2026-07-10T02:26:37.6149272Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035527642, "architect_striker", 15, 393, 0, 97, 52, 350.4998f, 102.8164f, 277.93817f, 0f, 0.96204597f, 0f, 0.27288744f, (SimpleCharFullUpdateFlags)36326091, 0, "00000000000000008000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-212336", "2026-07-10T02:33:47.2697443Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035568676, "architect_striker", 15, 393, 0, 97, 52, 335.68707f, 106.715f, 268.21643f, 0f, -0.8904477f, 0f, 0.4550851f, (SimpleCharFullUpdateFlags)36326091, 0, "80000000000000008000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-212336", "2026-07-10T02:31:28.0840211Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035568887, "architect_striker", 17, 459, 0, 98, 59, 334.87518f, 109.015f, 205.55635f, 0f, -0.88037777f, 0f, 0.47427312f, (SimpleCharFullUpdateFlags)36326091, 0, "80000000000000008000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-212336", "2026-07-10T02:33:47.2707446Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035569093, "bloodcreeper", 24, 691, 0, 70, 83, 221.83015f, 73.01637f, 99.27147f, 0f, 0.69924456f, 0f, 0.71488255f, (SimpleCharFullUpdateFlags)36391491, 0, "3FBFF3F83A4FE0A33D07DE7102020101000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[2]
		{
			new CapturedSubwayWaypointDefinition(221.83015f, 73.01637f, 99.27147f),
			new CapturedSubwayWaypointDefinition(224.47519f, 73.01795f, 99.329956f)
		}, "", "20260709-222339", "2026-07-10T03:28:28.9690500Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035762471, "deranged_shopper", 8, 256, 0, 94, 31, 255.7054f, 107.61169f, 285.02032f, 0f, -0.20919968f, 0f, 0.97787297f, (SimpleCharFullUpdateFlags)34294475, 0, "BF1CFF49BB8D859C3FAF117D02020101000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[2]
		{
			new CapturedSubwayWaypointDefinition(255.7054f, 107.61169f, 285.02032f),
			new CapturedSubwayWaypointDefinition(254.4f, 107.601685f, 287.89996f)
		}, "", "20260710-202132", "2026-07-11T01:23:01.1556318Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035569013, "empty_shell", 21, 474, 0, 99, 80, 171.54462f, 81.21325f, 65.037476f, 0f, 0.24117579f, 0f, 0.97048146f, (SimpleCharFullUpdateFlags)36391627, 0, "00000000000000000000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[1]
		{
			new CapturedSubwayWaypointDefinition(171.54462f, 81.21325f, 65.037476f)
		}, "", "20260709-222339", "2026-07-10T03:29:26.9002087Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035569016, "empty_shell", 18, 394, 0, 98, 68, 219.29808f, 80.615f, 43.026474f, 0f, -0.5330386f, 0f, 0.8460909f, (SimpleCharFullUpdateFlags)36326091, 0, "80000000000000000000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-222339", "2026-07-10T03:29:20.7520735Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035569017, "empty_shell", 19, 421, 0, 98, 72, 151.36826f, 77.21325f, 79.12771f, 0f, 0.5719257f, 0f, 0.8203054f, (SimpleCharFullUpdateFlags)36326091, 0, "00000000000000000000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-222339", "2026-07-10T03:29:26.9002087Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035569026, "empty_shell", 18, 394, 0, 98, 68, 188.7712f, 80.615f, 43.44335f, 0f, 0.9989379f, 0f, -0.046072483f, (SimpleCharFullUpdateFlags)36326091, 0, "80000000000000008000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-222339", "2026-07-10T03:29:20.7520735Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035569051, "empty_shell", 21, 474, 0, 99, 80, 161.4158f, 81.21325f, 80.84464f, 0f, 0.99998826f, 0f, 0.004858345f, (SimpleCharFullUpdateFlags)36326091, 0, "00000000000000008000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-222339", "2026-07-10T03:29:26.9002087Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035569002, "fragmented_soul", 17, 368, 0, 98, 59, 247.8775f, 80.815f, 89.73535f, 0f, -0.99804693f, 0f, 0.062475033f, (SimpleCharFullUpdateFlags)34228939, 0, "80000000000000008000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-222339", "2026-07-10T03:28:28.9690500Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035569007, "fragmented_soul", 17, 368, 0, 98, 59, 246.1502f, 80.8362f, 70.170006f, 0f, 0.7085661f, 0f, 0.70564437f, (SimpleCharFullUpdateFlags)34228939, 0, "00000000000000008000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-222339", "2026-07-10T03:29:20.7195387Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035569018, "fragmented_soul", 19, 421, 0, 98, 66, 221.32574f, 80.615f, 42.694427f, 0f, -0.6474544f, 0f, 0.7621042f, (SimpleCharFullUpdateFlags)34294475, 0, "80000000000000000000000003010001000100010001000000030000", 0, new CapturedSubwayWaypointDefinition[1]
		{
			new CapturedSubwayWaypointDefinition(221.32574f, 80.615f, 42.694427f)
		}, "", "20260709-222339", "2026-07-10T03:29:20.7520735Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035569034, "fragmented_soul", 20, 447, 0, 99, 69, 160.86247f, 81.214806f, 40.865f, 0f, -0.14733842f, 0f, 0.9890862f, (SimpleCharFullUpdateFlags)34228939, 0, "80000000000000000000000002010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-222339", "2026-07-10T03:29:24.8995230Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035569035, "fragmented_soul", 18, 394, 0, 98, 62, 159.6953f, 81.214806f, 44.695705f, 0f, 0.9890861f, 0f, 0.1473384f, (SimpleCharFullUpdateFlags)34228939, 0, "00000000000000008000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-222339", "2026-07-10T03:29:24.8995230Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035569038, "fragmented_soul", 18, 394, 0, 98, 62, 165.44876f, 81.265f, 50.18951f, 0f, -0.92104083f, 0f, 0.389466f, (SimpleCharFullUpdateFlags)34228939, 0, "00000000000000000000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-222339", "2026-07-10T03:29:26.9002087Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035569066, "fragmented_soul", 21, 474, 0, 99, 73, 112.67589f, 77.015f, 125.60569f, 0f, 0.0029946144f, 0f, 0.99999547f, (SimpleCharFullUpdateFlags)34228939, 0, "00000000000000000000000002010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-222339", "2026-07-10T03:30:11.4330927Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035569070, "fragmented_soul", 21, 474, 0, 99, 73, 112.69168f, 77.015f, 128.36195f, 0f, 0.99999595f, 0f, -0.0028648607f, (SimpleCharFullUpdateFlags)34228939, 0, "80000000000000008000000002010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-222339", "2026-07-10T03:30:11.4340964Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035569224, "fragmented_soul", 18, 394, 0, 98, 62, 246.94608f, 81.01639f, 117.82789f, 0f, -0.025276028f, 0f, 0.9996805f, (SimpleCharFullUpdateFlags)34228939, 0, "80000000000000000000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-222339", "2026-07-10T03:27:00.1252907Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035569511, "fragmented_soul", 18, 394, 0, 98, 62, 247.51184f, 81.60795f, 104.4165f, 0f, -0.3797401f, 0f, 0.92509323f, (SimpleCharFullUpdateFlags)34228939, 0, "80000000000000000000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-225408", "2026-07-10T04:04:16.7745570Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035569008, "incomplete_rebuild", 17, 368, 0, 98, 59, 247.28387f, 80.83573f, 70.16532f, 0f, -0.70564437f, 0f, 0.7085662f, (SimpleCharFullUpdateFlags)34228939, 0, "80000000000000000000000002010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-222339", "2026-07-10T03:29:20.7195387Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035569010, "incomplete_rebuild", 18, 394, 0, 98, 62, 246.54196f, 80.615f, 45.10734f, 0f, 0.9153552f, 0f, 0.40264747f, (SimpleCharFullUpdateFlags)34228939, 0, "00000000000000008000000002010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-222339", "2026-07-10T03:29:20.7205382Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035569015, "incomplete_rebuild", 19, 421, 0, 98, 66, 223.13367f, 80.615f, 44.145676f, 0f, 0.73240864f, 0f, -0.6808652f, (SimpleCharFullUpdateFlags)34228939, 0, "80000000000000008000000002010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-222339", "2026-07-10T03:29:20.7520735Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035569025, "incomplete_rebuild", 19, 421, 0, 98, 66, 189.38318f, 80.615f, 42.758408f, 0f, -0.91887516f, 0f, 0.39454818f, (SimpleCharFullUpdateFlags)34228939, 0, "80000000000000008000000002010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-222339", "2026-07-10T03:29:20.7520735Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035569032, "incomplete_rebuild", 19, 421, 0, 98, 66, 164.62663f, 81.214806f, 40.4079f, 0f, -0.4146376f, 0f, 0.90998656f, (SimpleCharFullUpdateFlags)34228939, 0, "80000000000000000000000002010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-222339", "2026-07-10T03:29:24.8995230Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035569084, "incomplete_rebuild", 21, 474, 0, 99, 73, 101.693535f, 73.01481f, 105.67212f, 0f, -0.6549027f, 0f, 0.7557133f, (SimpleCharFullUpdateFlags)34228939, 0, "80000000000000000000000002010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-222339", "2026-07-10T03:30:11.4340964Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035569089, "incomplete_rebuild", 19, 421, 0, 98, 66, 97.24542f, 73.01481f, 106.31116f, 0f, 0.49283585f, 0f, 0.8701224f, (SimpleCharFullUpdateFlags)34228939, 0, "00000000000000000000000002010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-222339", "2026-07-10T03:30:11.4340964Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035569099, "incomplete_rebuild", 21, 474, 0, 99, 73, 137.06435f, 73.01637f, 93.545975f, 0f, -0.79099053f, 0f, 0.61182845f, (SimpleCharFullUpdateFlags)34228939, 0, "80000000000000008000000002010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-222339", "2026-07-10T03:29:20.8121828Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035569149, "incomplete_rebuild", 19, 421, 0, 98, 66, 137.19351f, 73.01637f, 104.50207f, 0f, -0.43722016f, 0f, 0.8993544f, (SimpleCharFullUpdateFlags)34228939, 0, "80000000000000000000000002010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-222339", "2026-07-10T03:29:20.8121828Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035569217, "incomplete_rebuild", 17, 368, 0, 98, 59, 247.01f, 81.01639f, 117.9f, 0f, 0.86111283f, 0f, 0.5084138f, (SimpleCharFullUpdateFlags)34229963, 0, "00000000000000008000000003010001000100010001000000030000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-222339", "2026-07-10T03:25:38.4105867Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035526170, "infected_attendant", 11, 261, 0, 96, 38, 301.62085f, 102.81483f, 164.8918f, 0f, -0.59144694f, 0f, -0.80634356f, (SimpleCharFullUpdateFlags)36325955, 0, "00000000000000000000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-212336", "2026-07-10T02:26:37.6139282Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035526194, "infected_attendant", 15, 393, 0, 97, 52, 349.08603f, 102.8164f, 249.06523f, 0f, -0.9639409f, 0f, 0.26611635f, (SimpleCharFullUpdateFlags)36391491, 0, "80000000000000008000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[1]
		{
			new CapturedSubwayWaypointDefinition(349.08603f, 102.8164f, 249.06523f)
		}, "", "20260709-212336", "2026-07-10T02:26:37.6149272Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035526445, "infected_attendant", 11, 261, 0, 96, 38, 295.18524f, 102.81483f, 166.87564f, 0f, -0.69083565f, 0f, -0.7230115f, (SimpleCharFullUpdateFlags)36325955, 0, "00000000000000000000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-212336", "2026-07-10T02:26:37.6139282Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035569068, "infected_attendant", 23, 658, 0, 100, 80, 115.83011f, 73.01637f, 102.81289f, 0f, -0.9411577f, 0f, -0.33796805f, (SimpleCharFullUpdateFlags)36325955, 0, "00000000000000008000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-222339", "2026-07-10T03:29:20.7520735Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035569144, "infected_attendant", 12, 294, 0, 96, 41, 318.46304f, 102.8164f, 150.53802f, 0f, 0.3969985f, 0f, 0.91781926f, (SimpleCharFullUpdateFlags)36391491, 0, "3F8054903DFFD5843F712D7C02020101000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[2]
		{
			new CapturedSubwayWaypointDefinition(318.46304f, 102.8164f, 150.53802f),
			new CapturedSubwayWaypointDefinition(320.1f, 104.50609f, 154.83044f)
		}, "", "20260709-222339", "2026-07-10T03:24:04.2322101Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035527012, "infector", 19, 736, 0, 69, 53, 162.81444f, 81.315f, 89.36184f, 0f, -0.83373713f, 0f, 0.5521616f, (SimpleCharFullUpdateFlags)36391491, 0, "80000000000000008000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[1]
		{
			new CapturedSubwayWaypointDefinition(162.81444f, 81.315f, 89.36184f)
		}, "", "20260709-222339", "2026-07-10T03:29:26.9002087Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035568975, "infector", 17, 643, 0, 68, 47, 232.99f, 102.8164f, 173.66397f, 0f, 0.71044976f, 0f, 0.703748f, (SimpleCharFullUpdateFlags)36325955, 0, "00000000000000008000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-222339", "2026-07-10T03:24:00.0648983Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035568976, "infector", 16, 597, 0, 68, 44, 232.99f, 102.8164f, 179.16084f, 0f, 0.99999166f, 0f, 0.004078849f, (SimpleCharFullUpdateFlags)36391491, 0, "3C481C3138701A41BFBFA33602020101000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[2]
		{
			new CapturedSubwayWaypointDefinition(232.99f, 102.8164f, 179.16084f),
			new CapturedSubwayWaypointDefinition(232.92627f, 102.8164f, 174.36584f)
		}, "", "20260709-222339", "2026-07-10T03:24:00.0648983Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035568979, "infector", 17, 643, 0, 68, 47, 245.66652f, 102.8164f, 173.54382f, 0f, -0.1477899f, 0f, 0.98901916f, (SimpleCharFullUpdateFlags)36325955, 0, "80000000000000000000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-222339", "2026-07-10T03:24:00.0648983Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035569021, "infector", 19, 736, 0, 69, 53, 143.20459f, 81.315f, 89.34718f, 0f, 0.39260194f, 0f, 0.9197085f, (SimpleCharFullUpdateFlags)36391491, 0, "00000000000000000000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[1]
		{
			new CapturedSubwayWaypointDefinition(143.20459f, 81.315f, 89.34718f)
		}, "", "20260709-222339", "2026-07-10T03:29:26.9002087Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035569062, "infector", 17, 643, 0, 68, 47, 234.91495f, 78.66026f, 197.487f, 0f, 0.99980193f, 0f, 0.01990317f, (SimpleCharFullUpdateFlags)36391491, 0, "00000000000000008000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[1]
		{
			new CapturedSubwayWaypointDefinition(234.91495f, 78.66026f, 197.487f)
		}, "", "20260709-222339", "2026-07-10T03:24:04.2684007Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035569096, "infector", 20, 782, 0, 69, 55, 136.88487f, 73.115f, 113.12447f, 0f, -0.9697492f, 0f, 0.24410324f, (SimpleCharFullUpdateFlags)36325955, 0, "80000000000000008000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-222339", "2026-07-10T03:29:20.7520735Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035569097, "infector", 19, 736, 0, 69, 53, 136.89073f, 73.115f, 85.05395f, 0f, -0.010220608f, 0f, -0.9999477f, (SimpleCharFullUpdateFlags)36325955, 0, "00000000000000000000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-222339", "2026-07-10T03:29:20.7520735Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035569128, "infector", 25, 1014, 0, 70, 69, 298.0598f, 73.01795f, 98.918274f, 0f, 0.016408266f, 0f, 0.99986535f, (SimpleCharFullUpdateFlags)36391491, 0, "00000000000000000000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[1]
		{
			new CapturedSubwayWaypointDefinition(298.0598f, 73.01795f, 98.918274f)
		}, "", "20260709-222339", "2026-07-10T03:29:20.8121828Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035569130, "infector", 25, 1014, 0, 70, 69, 297.99f, 73.01795f, 88.562904f, 0f, 0.003371371f, 0f, 0.99999434f, (SimpleCharFullUpdateFlags)36391491, 0, "00000000000000000000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[1]
		{
			new CapturedSubwayWaypointDefinition(297.99f, 73.01795f, 88.562904f)
		}, "", "20260709-222339", "2026-07-10T03:29:20.8121828Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035569131, "infector", 25, 1014, 0, 70, 69, 298.42172f, 73.01795f, 109.94213f, 0f, 0.9998654f, 0f, -0.016407793f, (SimpleCharFullUpdateFlags)36391491, 0, "00000000000000000000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[1]
		{
			new CapturedSubwayWaypointDefinition(298.42172f, 73.01795f, 109.94213f)
		}, "", "20260709-222339", "2026-07-10T03:29:20.8121828Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035569176, "infector", 18, 690, 0, 69, 50, 259.24442f, 78.61415f, 197.63423f, 0f, -0.7218308f, 0f, 0.6920696f, (SimpleCharFullUpdateFlags)36391491, 0, "00000000000000000000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[1]
		{
			new CapturedSubwayWaypointDefinition(259.24442f, 78.61415f, 197.63423f)
		}, "", "20260709-222339", "2026-07-10T03:24:04.2684007Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035487452, "looter", 10, 318, 0, 95, 34, 241.13612f, 107.61169f, 301.13354f, 0f, -0.9715883f, 0f, 0.23667741f, (SimpleCharFullUpdateFlags)34228939, 0, "80000000000000008000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-212336", "2026-07-10T02:24:43.5161530Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035487691, "looter", 9, 287, 0, 95, 31, 209.07036f, 107.61169f, 295.3449f, 0f, 0.08720759f, 0f, 0.99619013f, (SimpleCharFullUpdateFlags)34228939, 0, "00000000000000000000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-212336", "2026-07-10T02:24:43.5161530Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035568667, "looter", 9, 287, 0, 95, 31, 193.17863f, 107.61169f, 313.5299f, 0f, -0.9666233f, 0f, 39f / (56f * (float)Math.E), (SimpleCharFullUpdateFlags)34228939, 0, "80000000000000008000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-212336", "2026-07-10T02:24:43.5161530Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035568681, "looter", 9, 287, 0, 95, 31, 222.92604f, 107.61169f, 304.15106f, 0f, -0.6967187f, 0f, 0.71734446f, (SimpleCharFullUpdateFlags)34294475, 0, "C01ABA353FE6DC633D9078A503020101000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[2]
		{
			new CapturedSubwayWaypointDefinition(222.92604f, 107.61169f, 304.15106f),
			new CapturedSubwayWaypointDefinition(227.31635f, 107.61169f, 304.24353f)
		}, "", "20260709-212336", "2026-07-10T02:24:43.5161530Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035568692, "looter", 10, 318, 0, 95, 34, 254.33493f, 107.61169f, 280.4771f, 0f, 0.75243026f, 0f, 0.6586719f, (SimpleCharFullUpdateFlags)34228939, 0, "00000000000000000000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-212336", "2026-07-10T02:24:43.5161530Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035568700, "looter", 10, 318, 0, 95, 34, 263.85785f, 107.715f, 285.41052f, 0f, -0.7057684f, 0f, 0.7084426f, (SimpleCharFullUpdateFlags)34294475, 0, "C0A3E286BD50D8B13C9EABC203020101000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[2]
		{
			new CapturedSubwayWaypointDefinition(263.85785f, 107.715f, 285.41052f),
			new CapturedSubwayWaypointDefinition(259.6285f, 107.61169f, 285.432f)
		}, "", "20260709-212336", "2026-07-10T02:24:37.0524810Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035645624, "looter", 10, 318, 0, 95, 34, 284.85077f, 107.61169f, 294.08563f, 0f, -0.9593532f, 0f, 0.2822098f, (SimpleCharFullUpdateFlags)34228939, 0, "80000000000000008000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260710-202132", "2026-07-11T01:23:43.2019952Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035803597, "looter", 9, 287, 0, 95, 31, 230.67459f, 107.61169f, 290.99f, 0f, 0.9820799f, 0f, 0.18846504f, (SimpleCharFullUpdateFlags)34294475, 0, "00000000000000000000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[1]
		{
			new CapturedSubwayWaypointDefinition(230.67459f, 107.61169f, 290.99f)
		}, "", "20260710-202132", "2026-07-11T01:23:01.1556318Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035527373, "lost_thought", 18, 493, 0, 98, 62, 235.25267f, 80.45897f, 177.8233f, 0f, -0.8625311f, 0f, -0.50600255f, (SimpleCharFullUpdateFlags)36325955, 0, "00000000000000008000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-222339", "2026-07-10T03:24:04.2684007Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035527378, "lost_thought", 16, 426, 0, 97, 55, 234.96611f, 80.81796f, 174.96895f, 0f, 0.83824265f, 0f, 0.5452981f, (SimpleCharFullUpdateFlags)36325955, 0, "00000000000000008000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-222339", "2026-07-10T03:24:04.2684007Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035569079, "lost_thought", 22, 625, 0, 99, 76, 101.22178f, 73.01481f, 117.40828f, 0f, -0.99979824f, 0f, -0.02008619f, (SimpleCharFullUpdateFlags)36325955, 0, "00000000000000008000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-222339", "2026-07-10T03:30:11.4340964Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035569088, "lost_thought", 21, 592, 0, 99, 73, 101.42485f, 73.01481f, 108.817024f, 0f, -0.87012225f, 0f, 0.49283585f, (SimpleCharFullUpdateFlags)36325955, 0, "80000000000000008000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-222339", "2026-07-10T03:30:11.4340964Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035568782, "melded_patterns", 23, 658, 0, 130, 80, 202.29802f, 69.01637f, 87.268616f, 0f, -0.038961582f, 0f, 0.9992407f, (SimpleCharFullUpdateFlags)34228939, 0, "80000000000000000000000002010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-222339", "2026-07-10T03:28:28.9690500Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035569020, "melded_patterns", 18, 493, 0, 130, 62, 137.01935f, 77.21325f, 73.5426f, 0f, 0.70361257f, 0f, 0.7105838f, (SimpleCharFullUpdateFlags)34294475, 0, "00000000000000000000000003010001000100010001000000030000", 0, new CapturedSubwayWaypointDefinition[1]
		{
			new CapturedSubwayWaypointDefinition(137.01935f, 77.21325f, 73.5426f)
		}, "", "20260709-222339", "2026-07-10T03:29:26.9002087Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035569029, "melded_patterns", 19, 526, 0, 130, 66, 172.95262f, 81.21325f, 63.496403f, 0f, -0.36175847f, 0f, 0.93227184f, (SimpleCharFullUpdateFlags)34294475, 0, "80000000000000000000000003010001000100010001000000030000", 0, new CapturedSubwayWaypointDefinition[1]
		{
			new CapturedSubwayWaypointDefinition(172.95262f, 81.21325f, 63.496403f)
		}, "", "20260709-222339", "2026-07-10T03:29:26.9002087Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035569031, "melded_patterns", 21, 592, 0, 130, 73, 131.02078f, 77.013245f, 74.987305f, 0f, -0.41414455f, 0f, 0.91021115f, (SimpleCharFullUpdateFlags)34228939, 0, "80000000000000000000000002010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-222339", "2026-07-10T03:29:47.0160225Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035569040, "melded_patterns", 18, 493, 0, 130, 62, 125.1441f, 77.058525f, 80.108284f, 0f, 0.91021115f, 0f, 0.41414452f, (SimpleCharFullUpdateFlags)34228939, 0, "00000000000000008000000002010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-222339", "2026-07-10T03:29:47.0160225Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035569046, "melded_patterns", 20, 559, 0, 130, 69, 122.97671f, 77.01481f, 117.59314f, 0f, -0.28547674f, 0f, 0.95838577f, (SimpleCharFullUpdateFlags)34228939, 0, "80000000000000000000000002010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-222339", "2026-07-10T03:30:07.5655221Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035569048, "melded_patterns", 21, 592, 0, 130, 73, 116.742966f, 77.01481f, 127.12849f, 0f, 0.95838565f, 0f, 0.28547674f, (SimpleCharFullUpdateFlags)34228939, 0, "00000000000000008000000002010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-222339", "2026-07-10T03:30:07.5655221Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035569082, "melded_patterns", 22, 625, 0, 130, 76, 122.84056f, 73.01637f, 105.31652f, 0f, 0.8571372f, 0f, 0.51508814f, (SimpleCharFullUpdateFlags)34228939, 0, "00000000000000000000000002010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-222339", "2026-07-10T03:29:20.7520735Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035569112, "melded_patterns", 25, 724, 0, 130, 86, 279.64755f, 73.01795f, 101.03623f, 0f, -0.68159676f, 0f, 0.73172796f, (SimpleCharFullUpdateFlags)34228939, 0, "80000000000000000000000002010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-222339", "2026-07-10T03:29:11.9700514Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035569117, "melded_patterns", 25, 724, 0, 130, 86, 279.72604f, 73.01795f, 97.10406f, 0f, -0.70248425f, 0f, 0.7116994f, (SimpleCharFullUpdateFlags)34228939, 0, "80000000000000000000000002010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-222339", "2026-07-10T03:29:11.9700514Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035568869, "molested_molecules", 20, 447, 0, 130, 69, 113.34185f, 73.01637f, 104.94499f, 0f, 0.76041645f, 0f, 0.64943594f, (SimpleCharFullUpdateFlags)34228939, 0, "00000000000000008000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-222339", "2026-07-10T03:29:20.7520735Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035568953, "molested_molecules", 23, 527, 0, 130, 80, 201.96426f, 69.01637f, 119.0757f, 0f, -0.97811556f, 0f, 0.2080623f, (SimpleCharFullUpdateFlags)34294475, 0, "BF1C33E8BB7A85C9BFAF45F302020101000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[2]
		{
			new CapturedSubwayWaypointDefinition(201.96426f, 69.01637f, 119.0757f),
			new CapturedSubwayWaypointDefinition(200.70001f, 69.00795f, 116.30002f)
		}, "", "20260709-222339", "2026-07-10T03:28:28.9690500Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035569012, "molested_molecules", 18, 394, 0, 130, 62, 248.04448f, 80.615f, 43.72994f, 0f, -0.40264744f, 0f, 0.9153551f, (SimpleCharFullUpdateFlags)34228939, 0, "80000000000000000000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-222339", "2026-07-10T03:29:20.7520735Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035569019, "molested_molecules", 17, 368, 0, 130, 59, 217.77719f, 80.615f, 43.754444f, 0f, 0.8460908f, 0f, 0.5330388f, (SimpleCharFullUpdateFlags)34228939, 0, "00000000000000008000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-222339", "2026-07-10T03:29:20.7520735Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035569077, "molested_molecules", 20, 447, 0, 130, 69, 98.74091f, 73.01481f, 135.153f, 0f, 0.2410718f, 0f, 0.97050726f, (SimpleCharFullUpdateFlags)34228939, 0, "00000000000000000000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-222339", "2026-07-10T03:30:11.4340964Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035569090, "molested_molecules", 21, 474, 0, 130, 73, 134.22275f, 73.01637f, 83.59113f, 0f, 0.85856485f, 0f, 0.51270497f, (SimpleCharFullUpdateFlags)34228939, 0, "00000000000000000000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-222339", "2026-07-10T03:29:20.7520735Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035569095, "molested_molecules", 19, 421, 0, 130, 66, 134.01f, 73.01637f, 114.808975f, 0f, 0.99819916f, 0f, -0.059985902f, (SimpleCharFullUpdateFlags)34228939, 0, "80000000000000008000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-222339", "2026-07-10T03:29:20.7520735Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035569106, "molested_molecules", 24, 553, 0, 130, 83, 237.20349f, 73.01637f, 94.18351f, 0f, 0.99686253f, 0f, 0.079152204f, (SimpleCharFullUpdateFlags)34294475, 0, "3E7260B8BB90FCDEBFBD951F02020101000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[2]
		{
			new CapturedSubwayWaypointDefinition(237.20349f, 73.01637f, 94.18351f),
			new CapturedSubwayWaypointDefinition(237.6f, 73.00795f, 91.70001f)
		}, "", "20260709-222339", "2026-07-10T03:28:28.9690500Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035569111, "molested_molecules", 24, 553, 0, 130, 83, 246.85274f, 73.01637f, 88.14026f, 0f, -0.5666547f, 0f, 0.8239553f, (SimpleCharFullUpdateFlags)34228939, 0, "80000000000000000000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-222339", "2026-07-10T03:28:28.9690500Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035569105, "neural_burnout", 22, 875, 0, 99, 76, 175.02226f, 73.01637f, 98.88357f, 0f, -0.40606537f, 0f, 0.913844f, (SimpleCharFullUpdateFlags)36326091, 0, "80000000000000000000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-222339", "2026-07-10T03:28:28.9690500Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035569109, "neural_burnout", 25, 1014, 0, 100, 86, 252.58379f, 73.01637f, 98.50631f, 0f, -0.9849552f, 0f, -0.17280982f, (SimpleCharFullUpdateFlags)36326091, 0, "00000000000000008000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-222339", "2026-07-10T03:28:28.9690500Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035569193, "neural_burnout", 17, 643, 0, 98, 59, 254.62538f, 81.01797f, 174.68025f, 0f, -0.76200205f, 0f, 0.6475746f, (SimpleCharFullUpdateFlags)36391627, 0, "BFBD7C54395B7994BE77BF6102020101000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[2]
		{
			new CapturedSubwayWaypointDefinition(254.62538f, 81.01797f, 174.68025f),
			new CapturedSubwayWaypointDefinition(252.52905f, 81.01797f, 174.33755f)
		}, "", "20260709-222339", "2026-07-10T03:24:04.2684007Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035569201, "neural_burnout", 17, 643, 0, 98, 59, 243.6672f, 79.01797f, 197.61769f, 0f, 0.6759477f, 0f, 0.73694956f, (SimpleCharFullUpdateFlags)36391627, 0, "3FBF41A73679E6C83E045DE202020101000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[2]
		{
			new CapturedSubwayWaypointDefinition(243.6672f, 79.01797f, 197.61769f),
			new CapturedSubwayWaypointDefinition(247.56369f, 79.01797f, 197.94705f)
		}, "", "20260709-222339", "2026-07-10T03:24:04.2684007Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035569213, "neural_burnout", 17, 643, 0, 98, 59, 243.79178f, 81.01639f, 153.94267f, 0f, 0.95644283f, 0f, 0.29191944f, (SimpleCharFullUpdateFlags)36326091, 0, "00000000000000008000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-222339", "2026-07-10T03:25:38.4105867Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035569221, "neural_burnout", 17, 643, 0, 98, 59, 250.76509f, 82.00606f, 147.3763f, 0f, -0.0038472638f, 0f, 0.9999926f, (SimpleCharFullUpdateFlags)36326091, 0, "80000000000000000000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-222339", "2026-07-10T03:25:50.3113102Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035569226, "neural_burnout", 18, 690, 0, 98, 62, 246.90152f, 82.006386f, 116.17879f, 0f, 0.0040712906f, 0f, 0.9999917f, (SimpleCharFullUpdateFlags)36326091, 0, "00000000000000000000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-222339", "2026-07-10T03:27:55.8549722Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035569001, "premature_pattern", 19, 421, 0, 98, 72, 246.32819f, 80.815f, 90.98379f, 0f, 0.9915708f, 0f, 0.12956654f, (SimpleCharFullUpdateFlags)36326091, 0, "00000000000000008000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-222339", "2026-07-10T03:28:28.9690500Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035569003, "premature_pattern", 16, 341, 0, 97, 61, 244.36592f, 81.01637f, 83.45188f, 0f, 0.92642784f, 0f, 0.37647238f, (SimpleCharFullUpdateFlags)36326091, 0, "00000000000000008000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-222339", "2026-07-10T03:29:11.9195060Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035569004, "premature_pattern", 18, 394, 0, 98, 68, 249.66173f, 81.01637f, 78.69549f, 0f, 0.40873727f, 0f, 0.91265213f, (SimpleCharFullUpdateFlags)36326091, 0, "00000000000000000000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-222339", "2026-07-10T03:29:11.9195060Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035569103, "premature_pattern", 22, 500, 0, 99, 84, 206.58656f, 73.01637f, 132.43826f, 0f, -0.91504276f, 0f, 0.4033569f, (SimpleCharFullUpdateFlags)36391627, 0, "80000000000000008000000002010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[1]
		{
			new CapturedSubwayWaypointDefinition(206.58656f, 73.01637f, 132.43826f)
		}, "", "20260709-222339", "2026-07-10T03:28:28.9690500Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035569253, "premature_pattern", 17, 368, 0, 98, 65, 235.90584f, 80.81796f, 175.16763f, 0f, -0.77726096f, 0f, 0.62917835f, (SimpleCharFullUpdateFlags)36391627, 0, "BFBA0303BD45D3BDBE9E6FB302020101000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[2]
		{
			new CapturedSubwayWaypointDefinition(235.90584f, 80.81796f, 175.16763f),
			new CapturedSubwayWaypointDefinition(234.80005f, 80.799995f, 174.8f)
		}, "", "20260709-222339", "2026-07-10T03:30:53.8633703Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035569272, "premature_pattern", 18, 394, 0, 98, 68, 246.10957f, 81.607956f, 106.61589f, 0f, -0.995765f, 0f, 0.09193572f, (SimpleCharFullUpdateFlags)36326091, 0, "80000000000000008000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-222339", "2026-07-10T03:31:54.0130240Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035569494, "premature_pattern", 18, 394, 0, 98, 68, 246.99f, 81.01639f, 116.977585f, 0f, 0.999957f, 0f, 0.009270155f, (SimpleCharFullUpdateFlags)36391627, 0, "3CE3D003BB87C3E1BFBFF75702020101000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[14]
		{
			new CapturedSubwayWaypointDefinition(246.99f, 81.01639f, 116.977585f),
			new CapturedSubwayWaypointDefinition(247.10005f, 80.99999f, 111.4f),
			new CapturedSubwayWaypointDefinition(247.10005f, 80.99999f, 108.3f),
			new CapturedSubwayWaypointDefinition(247.1f, 81f, 87.5f),
			new CapturedSubwayWaypointDefinition(247.10005f, 80.99999f, 85.1f),
			new CapturedSubwayWaypointDefinition(249.50005f, 80.99999f, 84.4f),
			new CapturedSubwayWaypointDefinition(243.90005f, 80.99999f, 76.4f),
			new CapturedSubwayWaypointDefinition(250.00005f, 80.99999f, 76.3f),
			new CapturedSubwayWaypointDefinition(243.90005f, 80.99999f, 76.4f),
			new CapturedSubwayWaypointDefinition(249.50005f, 80.99999f, 84.4f),
			new CapturedSubwayWaypointDefinition(247.10005f, 80.99999f, 85.1f),
			new CapturedSubwayWaypointDefinition(247.1f, 81f, 87.5f),
			new CapturedSubwayWaypointDefinition(247.10005f, 80.99999f, 108.3f),
			new CapturedSubwayWaypointDefinition(247.10005f, 80.99999f, 111.4f)
		}, "", "20260709-225408", "2026-07-10T04:04:42.0809910Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035527557, "redundant_scan", 20, 782, 0, 99, 69, 123.94272f, 73.01637f, 92.973694f, 0f, -0.7290873f, 0f, 0.68442076f, (SimpleCharFullUpdateFlags)34228939, 0, "80000000000000008000000002010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-222339", "2026-07-10T03:29:20.7520735Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035569087, "redundant_scan", 19, 736, 0, 98, 66, 87.743355f, 73.01481f, 136.22421f, 0f, 0.69957966f, 0f, 0.7145546f, (SimpleCharFullUpdateFlags)34228939, 0, "00000000000000000000000002010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-222339", "2026-07-10T03:30:11.4340964Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035569092, "redundant_scan", 21, 829, 0, 99, 73, 130.32594f, 73.5004f, 109.9726f, 0f, 0.6967455f, 0f, 0.7173184f, (SimpleCharFullUpdateFlags)34294475, 0, "3FBED60ABE228D9F3D31BAB202020101000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[2]
		{
			new CapturedSubwayWaypointDefinition(130.32594f, 73.5004f, 109.9726f),
			new CapturedSubwayWaypointDefinition(134.70001f, 73.00637f, 110.100006f)
		}, "", "20260709-222339", "2026-07-10T03:29:20.7520735Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035569107, "redundant_scan", 19, 736, 0, 98, 66, 214.69275f, 73.01637f, 87.13444f, 0f, -0.00425591f, 0f, 0.99999094f, (SimpleCharFullUpdateFlags)34228939, 0, "80000000000000000000000002010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-222339", "2026-07-10T03:28:28.9690500Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035451915, "shadow", 9, 205, 0, 95, 25, 184.22253f, 108.416405f, 213.63098f, 0f, -0.6213651f, 0f, 0.7835212f, (SimpleCharFullUpdateFlags)36391491, 0, "BFA0AFBCBC610AC73E9660F202020101000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[2]
		{
			new CapturedSubwayWaypointDefinition(184.22253f, 108.416405f, 213.63098f),
			new CapturedSubwayWaypointDefinition(183.5f, 108.4064f, 213.80002f)
		}, "", "20260709-212336", "2026-07-10T02:24:43.4685504Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035451927, "shadow", 10, 227, 0, 95, 27, 177.60059f, 108.416405f, 204.12949f, 0f, 0.2243696f, 0f, 0.97450423f, (SimpleCharFullUpdateFlags)36325955, 0, "00000000000000000000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-212336", "2026-07-10T02:24:43.4685504Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035451944, "shadow", 10, 227, 0, 95, 27, 184.23859f, 108.416405f, 203.42209f, 0f, 0.15920334f, 0f, 0.98724586f, (SimpleCharFullUpdateFlags)36325955, 0, "00000000000000000000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-212336", "2026-07-10T02:24:43.4685504Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035451945, "shadow", 10, 227, 0, 95, 27, 185.7629f, 108.416405f, 213.69414f, 0f, 0.84217554f, 0f, 0.5392025f, (SimpleCharFullUpdateFlags)36325955, 0, "00000000000000008000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-212336", "2026-07-10T02:24:43.4685504Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035451946, "shadow", 9, 205, 0, 95, 25, 181.87474f, 108.416405f, 213.7773f, 0f, -0.791875f, 0f, 0.6106832f, (SimpleCharFullUpdateFlags)36391491, 0, "BFAFD8A2BC758996BEB8D20602020101000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[2]
		{
			new CapturedSubwayWaypointDefinition(181.87474f, 108.416405f, 213.7773f),
			new CapturedSubwayWaypointDefinition(181.2f, 108.4064f, 213.6f)
		}, "", "20260709-212336", "2026-07-10T02:24:43.4685504Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035525996, "shadow", 15, 393, 0, 97, 41, 325.13107f, 102.8164f, 230.68593f, 0f, 0.9665778f, 0f, -0.25637352f, (SimpleCharFullUpdateFlags)36325955, 0, "80000000000000008000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-212336", "2026-07-10T02:31:28.0840211Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035526010, "shadow", 14, 360, 0, 97, 39, 323.38046f, 102.8164f, 229.93124f, 0f, 0.9981811f, 0f, -0.06028701f, (SimpleCharFullUpdateFlags)36325955, 0, "80000000000000008000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-212336", "2026-07-10T02:31:28.0840211Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035526076, "shadow", 13, 327, 0, 96, 36, 276.53058f, 102.8164f, 248.75395f, 0f, -0.57597095f, 0f, 0.8174699f, (SimpleCharFullUpdateFlags)36325955, 0, "80000000000000000000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-212336", "2026-07-10T02:25:40.9833693Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035526165, "shadow", 11, 261, 0, 96, 30, 214.0164f, 107.6164f, 252.20763f, 0f, -0.75861466f, 0f, 0.65154004f, (SimpleCharFullUpdateFlags)36325955, 0, "80000000000000008000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-212336", "2026-07-10T02:24:43.4685504Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035526172, "shadow", 10, 227, 0, 95, 27, 199.95602f, 107.6164f, 253.90077f, 0f, 0.9995353f, 0f, 0.030483628f, (SimpleCharFullUpdateFlags)36325955, 0, "00000000000000008000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-212336", "2026-07-10T02:24:43.4685504Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035526176, "shadow", 14, 360, 0, 97, 39, 283.02725f, 102.8164f, 248.95232f, 0f, 0.5058021f, 0f, 0.86264974f, (SimpleCharFullUpdateFlags)36325955, 0, "00000000000000000000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-212336", "2026-07-10T02:25:40.9833693Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035526179, "shadow", 14, 360, 0, 97, 39, 277.0984f, 102.8164f, 252.79239f, 0f, 0.7678455f, 0f, 0.640635f, (SimpleCharFullUpdateFlags)36325955, 0, "00000000000000008000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-212336", "2026-07-10T02:25:40.9833693Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035526181, "shadow", 11, 261, 0, 96, 30, 213.51508f, 107.6164f, 248.74504f, 0f, -0.6543974f, 0f, 0.75615114f, (SimpleCharFullUpdateFlags)36325955, 0, "80000000000000000000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-212336", "2026-07-10T02:24:43.4685504Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035526184, "shadow", 13, 327, 0, 96, 36, 281.55576f, 102.8164f, 252.31941f, 0f, -0.68912095f, 0f, 0.72464645f, (SimpleCharFullUpdateFlags)36325955, 0, "80000000000000000000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-212336", "2026-07-10T02:25:40.9833693Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035526186, "shadow", 13, 327, 0, 96, 36, 254.76277f, 105.0695f, 251.74107f, 0f, -0.67589664f, 0f, 0.7369964f, (SimpleCharFullUpdateFlags)36391491, 0, "00000000000000000000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[1]
		{
			new CapturedSubwayWaypointDefinition(254.76277f, 105.0695f, 251.74107f)
		}, "", "20260709-212336", "2026-07-10T02:25:40.9833693Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035526187, "shadow", 15, 393, 0, 97, 41, 251.44528f, 105.733f, 250.9119f, 0f, -0.6497367f, 0f, 0.7601593f, (SimpleCharFullUpdateFlags)36391491, 0, "80000000000000000000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[1]
		{
			new CapturedSubwayWaypointDefinition(251.44528f, 105.733f, 250.9119f)
		}, "", "20260709-212336", "2026-07-10T02:25:40.9833693Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035526195, "shadow", 14, 360, 0, 97, 39, 253.5944f, 105.30318f, 249.44415f, 0f, -0.6021418f, 0f, 0.79838914f, (SimpleCharFullUpdateFlags)36391491, 0, "80000000000000000000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[1]
		{
			new CapturedSubwayWaypointDefinition(253.5944f, 105.30318f, 249.44415f)
		}, "", "20260709-212336", "2026-07-10T02:25:40.9833693Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035526198, "shadow", 15, 393, 0, 97, 41, 279.02216f, 102.8164f, 251.05728f, 0f, 0.65337104f, 0f, -0.75703794f, (SimpleCharFullUpdateFlags)36325955, 0, "80000000000000000000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-212336", "2026-07-10T02:25:40.9833693Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035526225, "shadow", 11, 261, 0, 96, 30, 211.76683f, 107.6164f, 251.33885f, 0f, 0.78245413f, 0f, -0.62270796f, (SimpleCharFullUpdateFlags)36325955, 0, "80000000000000008000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-212336", "2026-07-10T02:24:43.4685504Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035526227, "shadow", 10, 227, 0, 95, 27, 197.72787f, 107.6164f, 253.73367f, 0f, 0.9998208f, 0f, -0.01892764f, (SimpleCharFullUpdateFlags)36325955, 0, "80000000000000008000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-212336", "2026-07-10T02:24:43.4695503Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035526229, "shadow", 9, 205, 0, 95, 25, 196.84991f, 108.416405f, 219.03362f, 0f, 0.92380357f, 0f, -0.38286602f, (SimpleCharFullUpdateFlags)36325955, 0, "80000000000000008000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-212336", "2026-07-10T02:24:43.4695503Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035526230, "shadow", 11, 261, 0, 96, 30, 201.19165f, 108.465f, 218.66505f, 0f, 0.99871707f, 0f, -0.050632697f, (SimpleCharFullUpdateFlags)36325955, 0, "80000000000000008000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-212336", "2026-07-10T02:24:43.4695503Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035527624, "shadow", 22, 625, 0, 99, 61, 261.8527f, 73.01795f, 95.97059f, 0f, -0.8390049f, 0f, 0.5441239f, (SimpleCharFullUpdateFlags)36391491, 0, "00000000000000000000000002010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[1]
		{
			new CapturedSubwayWaypointDefinition(261.8527f, 73.01795f, 95.97059f)
		}, "", "20260709-222339", "2026-07-10T03:29:11.9195060Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035527667, "shadow", 21, 592, 0, 99, 58, 158.68059f, 73.01637f, 93.15929f, 0f, 0.9489551f, 0f, 0.31541112f, (SimpleCharFullUpdateFlags)36391491, 0, "00000000000000000000000002010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[1]
		{
			new CapturedSubwayWaypointDefinition(158.68059f, 73.01637f, 93.15929f)
		}, "", "20260709-222339", "2026-07-10T03:29:11.9195060Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035527668, "shadow", 22, 625, 0, 99, 61, 158.70644f, 73.01637f, 105.07696f, 0f, -0.9440956f, 0f, 0.32967174f, (SimpleCharFullUpdateFlags)36391491, 0, "80000000000000008000000002010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[1]
		{
			new CapturedSubwayWaypointDefinition(158.70644f, 73.01637f, 105.07696f)
		}, "", "20260709-222339", "2026-07-10T03:29:11.9195060Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035527670, "shadow", 22, 625, 0, 99, 61, 149.50572f, 73.01637f, 104.86771f, 0f, -0.33059996f, 0f, 0.943771f, (SimpleCharFullUpdateFlags)36391491, 0, "00000000000000000000000002010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[1]
		{
			new CapturedSubwayWaypointDefinition(149.50572f, 73.01637f, 104.86771f)
		}, "", "20260709-222339", "2026-07-10T03:29:11.9195060Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035527671, "shadow", 21, 592, 0, 99, 58, 149.12103f, 73.01637f, 93.14485f, 0f, -0.94014776f, 0f, 0.3407671f, (SimpleCharFullUpdateFlags)36391491, 0, "00000000000000000000000002010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[1]
		{
			new CapturedSubwayWaypointDefinition(149.12103f, 73.01637f, 93.14485f)
		}, "", "20260709-222339", "2026-07-10T03:29:11.9195060Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035568691, "shadow", 24, 691, 0, 100, 67, 261.16605f, 73.01795f, 100.403046f, 0f, -0.71359086f, 0f, 0.7005627f, (SimpleCharFullUpdateFlags)36391491, 0, "00000000000000000000000002010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[1]
		{
			new CapturedSubwayWaypointDefinition(261.16605f, 73.01795f, 100.403046f)
		}, "", "20260709-222339", "2026-07-10T03:29:11.9195060Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035568696, "shadow", 24, 691, 0, 100, 67, 261.07724f, 73.01795f, 97.8602f, 0f, -0.81079304f, 0f, 0.58533293f, (SimpleCharFullUpdateFlags)36391491, 0, "00000000000000000000000002010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[1]
		{
			new CapturedSubwayWaypointDefinition(261.07724f, 73.01795f, 97.8602f)
		}, "", "20260709-222339", "2026-07-10T03:29:11.9195060Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035568698, "shadow", 24, 691, 0, 100, 67, 264.32657f, 73.01795f, 102.157135f, 0f, 0.7126372f, 0f, 0.7015328f, (SimpleCharFullUpdateFlags)36391491, 0, "00000000000000000000000002010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[1]
		{
			new CapturedSubwayWaypointDefinition(264.32657f, 73.01795f, 102.157135f)
		}, "", "20260709-222339", "2026-07-10T03:29:11.9195060Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035568901, "shadow", 11, 261, 0, 96, 30, 178.08875f, 108.416405f, 208.61945f, 0f, 0.7231683f, 0f, 0.69067186f, (SimpleCharFullUpdateFlags)36325955, 0, "00000000000000008000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-212336", "2026-07-10T02:35:12.1435724Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035526035, "slum_runner", 16, 426, 0, 97, 220, 305.7059f, 100.8164f, 207.60794f, 0f, 0.99347454f, 0f, 0.11405402f, (SimpleCharFullUpdateFlags)36391491, 0, "3EAE0A74B28B9E4ABFBB006302020101000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[2]
		{
			new CapturedSubwayWaypointDefinition(305.7059f, 100.8164f, 207.60794f),
			new CapturedSubwayWaypointDefinition(306.60236f, 100.8164f, 203.75269f)
		}, "", "20260709-212336", "2026-07-10T02:33:47.2707446Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035526144, "slum_runner", 15, 393, 0, 97, 206, 374.28665f, 100.8164f, 211.26031f, 0f, -0.57336026f, 0f, 0.81930333f, (SimpleCharFullUpdateFlags)36325955, 0, "00000000000000000000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-212336", "2026-07-10T02:33:47.2707446Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035526169, "slum_runner", 17, 459, 0, 98, 234, 347.1948f, 102.8164f, 233.60127f, 0f, -0.010622214f, 0f, 0.99994355f, (SimpleCharFullUpdateFlags)36325955, 0, "00000000000000000000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-212336", "2026-07-10T02:31:28.0840211Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035526180, "slum_runner", 16, 426, 0, 97, 220, 365.83655f, 100.965f, 201.5005f, 0f, -0.74555403f, 0f, 0.6664452f, (SimpleCharFullUpdateFlags)36325955, 0, "80000000000000008000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-212336", "2026-07-10T02:33:47.2707446Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035526182, "slum_runner", 15, 393, 0, 97, 206, 362.75333f, 100.8164f, 212.50395f, 0f, -0.5745399f, 0f, 0.81847656f, (SimpleCharFullUpdateFlags)36325955, 0, "80000000000000000000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-212336", "2026-07-10T02:33:47.2707446Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035526580, "slum_runner", 15, 393, 0, 97, 206, 351.12302f, 100.8164f, 200.46776f, 0f, 0.71009165f, 0f, 0.70410925f, (SimpleCharFullUpdateFlags)36391491, 0, "3FBFFE3E250820DABC4FEBA302020101000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[2]
		{
			new CapturedSubwayWaypointDefinition(351.12302f, 100.8164f, 200.46776f),
			new CapturedSubwayWaypointDefinition(353.75586f, 100.8164f, 200.44547f)
		}, "", "20260709-212336", "2026-07-10T02:33:47.2707446Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035527547, "slum_runner", 16, 426, 0, 97, 220, 236.45056f, 80.86702f, 175.19986f, -0.03818921f, 0.7656887f, 0.031984184f, 0.6412791f, (SimpleCharFullUpdateFlags)36325955, 0, "00000000000000008000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-222339", "2026-07-10T03:24:04.2684007Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035568728, "slum_runner", 21, 592, 0, 99, 289, 197.10547f, 73.01637f, 66.02543f, 0f, -0.10706205f, 0f, -0.99425244f, (SimpleCharFullUpdateFlags)36334147, 0, "00000000000000000000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-222339", "2026-07-10T03:29:11.9195060Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035568729, "slum_runner", 22, 625, 0, 99, 303, 194.71054f, 73.01637f, 66.27216f, 0f, -0.36035737f, 0f, -0.9328144f, (SimpleCharFullUpdateFlags)36334147, 0, "00000000000000000000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-222339", "2026-07-10T03:29:11.9195060Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035568736, "slum_runner", 21, 592, 0, 99, 289, 190.01825f, 73.01637f, 63.43316f, 0f, 0.6650148f, 0f, 0.74683034f, (SimpleCharFullUpdateFlags)36334147, 0, "00000000000000000000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-222339", "2026-07-10T03:29:11.9195060Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035568740, "slum_runner", 23, 658, 0, 100, 317, 192.6878f, 73.01637f, 59.716747f, 0f, 0.44698113f, 0f, 0.8945436f, (SimpleCharFullUpdateFlags)36334147, 0, "00000000000000000000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-222339", "2026-07-10T03:29:11.9195060Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035568748, "slum_runner", 21, 592, 0, 99, 289, 190.63353f, 73.116104f, 52.92437f, 0.1373398f, 0.6861222f, -0.0027538f, 0.71439964f, (SimpleCharFullUpdateFlags)36334147, 0, "00000000800000000000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-222339", "2026-07-10T03:29:20.7195387Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035568751, "slum_runner", 21, 592, 0, 99, 289, 190.41353f, 73.32652f, 51.073254f, -0.04640125f, 0.3611811f, -0.018027991f, 0.93116605f, (SimpleCharFullUpdateFlags)36334147, 0, "00000000000000000000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-222339", "2026-07-10T03:29:20.7195387Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035568755, "slum_runner", 21, 592, 0, 99, 289, 192.19258f, 73.01637f, 53.016476f, 0f, -0.72765905f, 0f, 0.68593913f, (SimpleCharFullUpdateFlags)36334147, 0, "80000000000000008000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-222339", "2026-07-10T03:29:20.7195387Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035568768, "slum_runner", 21, 592, 0, 99, 289, 237.03674f, 73.90235f, 51.738083f, -0.24178927f, 0.8372443f, 0.036096487f, 0.48913902f, (SimpleCharFullUpdateFlags)36399683, 0, "00000000000000008000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[1]
		{
			new CapturedSubwayWaypointDefinition(237.03674f, 73.90235f, 51.738083f)
		}, "", "20260709-222339", "2026-07-10T03:29:20.7195387Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035568769, "slum_runner", 20, 559, 0, 99, 275, 238.2151f, 74.84455f, 52.687813f, -0.25566852f, 0.9516131f, -0.17048769f, -0.00017253305f, (SimpleCharFullUpdateFlags)36399683, 0, "00000000800000008000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[1]
		{
			new CapturedSubwayWaypointDefinition(238.2151f, 74.84455f, 52.687813f)
		}, "", "20260709-222339", "2026-07-10T03:29:20.7195387Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035568771, "slum_runner", 22, 625, 0, 99, 303, 241.2254f, 73.858536f, 49.041992f, -0.2342167f, -0.46221203f, 0.12804712f, 0.8456397f, (SimpleCharFullUpdateFlags)36399683, 0, "00000000000000000000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[1]
		{
			new CapturedSubwayWaypointDefinition(241.2254f, 73.858536f, 49.041992f)
		}, "", "20260709-222339", "2026-07-10T03:29:20.7195387Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035568873, "slum_runner", 23, 658, 0, 100, 317, 199.93507f, 73.01637f, 68.36465f, 0f, -0.90407956f, 0f, 0.42736462f, (SimpleCharFullUpdateFlags)36334147, 0, "80000000000000008000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-222339", "2026-07-10T03:29:11.9195060Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035568886, "slum_runner", 23, 658, 0, 100, 317, 200.3223f, 73.01637f, 78.875206f, 0f, -0.6609008f, 0f, 0.7504733f, (SimpleCharFullUpdateFlags)36399683, 0, "BF960AB73DC7BB013E18F95B02020101000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[2]
		{
			new CapturedSubwayWaypointDefinition(200.3223f, 73.01637f, 78.875206f),
			new CapturedSubwayWaypointDefinition(198.92441f, 73.01795f, 79.05325f)
		}, "", "20260709-222339", "2026-07-10T03:28:28.9690500Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035568888, "slum_runner", 23, 658, 0, 100, 317, 206.23506f, 73.01637f, 79.09826f, 0f, -0.16457912f, 0f, 0.9863639f, (SimpleCharFullUpdateFlags)36334147, 0, "80000000000000000000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-222339", "2026-07-10T03:28:28.9690500Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035568890, "slum_runner", 21, 592, 0, 99, 289, 201.62973f, 73.01637f, 70.85812f, 0f, 0.9766015f, 0f, 0.21505718f, (SimpleCharFullUpdateFlags)36399683, 0, "3F20B5EE3AD26A58BFAD9AAF02020101000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[2]
		{
			new CapturedSubwayWaypointDefinition(201.62973f, 73.01637f, 70.85812f),
			new CapturedSubwayWaypointDefinition(202.24432f, 73.01637f, 69.527985f)
		}, "", "20260709-222339", "2026-07-10T03:29:11.9195060Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035568893, "slum_runner", 22, 625, 0, 99, 303, 197.62375f, 73.01637f, 68.404076f, 0f, -0.9463941f, 0f, 0.323015f, (SimpleCharFullUpdateFlags)36334147, 0, "80000000000000008000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-222339", "2026-07-10T03:29:11.9195060Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035569153, "slum_runner", 11, 261, 0, 96, 150, 335.77707f, 102.865f, 146.83302f, 0f, -0.6926414f, 0f, 0.7212821f, (SimpleCharFullUpdateFlags)36325955, 0, "80000000000000000000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-222339", "2026-07-10T03:24:04.2322101Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035569268, "slum_runner", 16, 426, 0, 97, 220, 247.30829f, 81.01797f, 164.04099f, 0f, -0.19808084f, 0f, 0.9801857f, (SimpleCharFullUpdateFlags)36325955, 0, "80000000000000000000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-222339", "2026-07-10T03:31:41.6974451Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035526219, "stim_fiend", 13, 327, 0, 96, 58, 224.2621f, 107.6164f, 252.70314f, 0f, 0.8279607f, 0f, -0.56078625f, (SimpleCharFullUpdateFlags)36326091, 0, "80000000000000008000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-212336", "2026-07-10T02:25:40.9833693Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035526573, "stim_fiend", 17, 459, 0, 98, 76, 347.3757f, 102.8164f, 222.27428f, 0f, -0.7069323f, 0f, 0.7072812f, (SimpleCharFullUpdateFlags)36326091, 0, "80000000000000000000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-212336", "2026-07-10T02:33:47.2707446Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035526591, "stim_fiend", 14, 360, 0, 97, 63, 335.84467f, 106.765f, 135.21626f, 0f, -0.12363024f, 0f, 0.99232835f, (SimpleCharFullUpdateFlags)36391627, 0, "80000000000000000000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[1]
		{
			new CapturedSubwayWaypointDefinition(335.84467f, 106.765f, 135.21626f)
		}, "", "20260709-222339", "2026-07-10T03:24:04.2322101Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035527014, "stim_fiend", 10, 227, 0, 95, 44, 197.26959f, 107.6164f, 176.21643f, 0f, -0.99778867f, 0f, 0.06646604f, (SimpleCharFullUpdateFlags)36391627, 0, "BE4BBB7E342F6CE4BFBE4DA102020101000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[2]
		{
			new CapturedSubwayWaypointDefinition(197.26959f, 107.6164f, 176.21643f),
			new CapturedSubwayWaypointDefinition(196.79018f, 107.6164f, 172.63232f)
		}, "", "20260709-212336", "2026-07-10T02:24:43.4950682Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035527016, "stim_fiend", 12, 294, 0, 96, 54, 275.33478f, 102.8164f, 167.14702f, 0f, 0.81929076f, 0f, -0.5733776f, (SimpleCharFullUpdateFlags)36326091, 0, "80000000000000008000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-212336", "2026-07-10T02:25:40.9833693Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035527037, "stim_fiend", 13, 327, 0, 96, 58, 278.70282f, 102.8164f, 164.7313f, 0f, 0.8100545f, 0f, 0.5863546f, (SimpleCharFullUpdateFlags)36391627, 0, "3FB62B2E3617DE24BEEF9C8B02020101000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[2]
		{
			new CapturedSubwayWaypointDefinition(278.70282f, 102.8164f, 164.7313f),
			new CapturedSubwayWaypointDefinition(280.4072f, 102.8164f, 164.17995f)
		}, "", "20260709-212336", "2026-07-10T02:25:40.9833693Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035568745, "stim_fiend", 12, 294, 0, 96, 54, 285.6103f, 108.60128f, 301.4973f, 0f, 0.99902093f, 0f, 0.044239447f, (SimpleCharFullUpdateFlags)36326091, 0, "00000000000000008000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-212336", "2026-07-10T02:23:52.7570421Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035568754, "stim_fiend", 12, 294, 0, 96, 54, 285.6248f, 107.61169f, 309.4349f, 0f, -0.25949845f, 0f, 0.96574354f, (SimpleCharFullUpdateFlags)36326091, 0, "80000000000000000000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-212336", "2026-07-10T02:24:10.5712217Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035569141, "stim_fiend", 13, 327, 0, 96, 58, 332.9645f, 106.715f, 142.63495f, 0f, -0.99705595f, 0f, 0.07667766f, (SimpleCharFullUpdateFlags)36391627, 0, "BE6AB1563D57C6C9BFBD9B6202020101000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[2]
		{
			new CapturedSubwayWaypointDefinition(332.9645f, 106.715f, 142.63495f),
			new CapturedSubwayWaypointDefinition(332.6f, 106.8064f, 140.4f)
		}, "", "20260709-222339", "2026-07-10T03:24:04.2322101Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035646226, "stim_fiend", 11, 261, 0, 96, 49, 287.73398f, 107.61169f, 299.43723f, 0f, -0.9918657f, 0f, 0.12728895f, (SimpleCharFullUpdateFlags)36391627, 0, "BEC1EC1A33FA5853BFB9C67202020101000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[2]
		{
			new CapturedSubwayWaypointDefinition(287.73398f, 107.61169f, 299.43723f),
			new CapturedSubwayWaypointDefinition(287.3037f, 107.61169f, 297.78726f)
		}, "", "20260710-202132", "2026-07-11T01:23:43.2019952Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035802408, "stim_fiend", 12, 294, 0, 96, 54, 287.05505f, 107.61169f, 310.95184f, 0f, -0.99816906f, 0f, 0.06048951f, (SimpleCharFullUpdateFlags)36326091, 0, "80000000000000008000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260710-202132", "2026-07-11T01:23:43.2019952Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035803157, "stim_fiend", 9, 205, 0, 95, 40, 197.72359f, 107.6164f, 168.28008f, 0f, 0.9979032f, 0f, 0.064723775f, (SimpleCharFullUpdateFlags)36391627, 0, "3D5BED123F0A3BE6BED3088102020101000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[2]
		{
			new CapturedSubwayWaypointDefinition(197.72359f, 107.6164f, 168.28008f),
			new CapturedSubwayWaypointDefinition(196.79018f, 107.6164f, 172.63232f)
		}, "", "20260710-202132", "2026-07-11T01:22:15.2253676Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035803599, "stim_fiend", 10, 227, 0, 95, 44, 277.57507f, 107.61169f, 275.63303f, 0f, 0.48089784f, 0f, 0.8767766f, (SimpleCharFullUpdateFlags)36326091, 0, "00000000000000000000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260710-202132", "2026-07-11T01:23:43.2019952Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035803600, "stim_fiend", 10, 227, 0, 95, 44, 290.75827f, 107.61169f, 283.75302f, 0f, 0.070782915f, 0f, 0.9974919f, (SimpleCharFullUpdateFlags)36326091, 0, "00000000000000000000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260710-202132", "2026-07-11T01:23:43.2339955Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035803601, "stim_fiend", 10, 227, 0, 95, 44, 292.3236f, 107.61169f, 294.72708f, 0f, -0.99412614f, 0f, 0.10822783f, (SimpleCharFullUpdateFlags)36326091, 0, "80000000000000008000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260710-202132", "2026-07-11T01:23:43.2339955Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035527013, "uncontrollable_anger", 13, 327, 0, 96, 45, 275.90695f, 102.8164f, 165.01192f, 0f, -0.70943224f, 0f, 0.70477366f, (SimpleCharFullUpdateFlags)36391491, 0, "BFBFFD77BC3C5FD7BC21E84F02020101000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[2]
		{
			new CapturedSubwayWaypointDefinition(275.90695f, 102.8164f, 165.01192f),
			new CapturedSubwayWaypointDefinition(274.1f, 102.8f, 165f)
		}, "", "20260709-212336", "2026-07-10T02:25:40.9833693Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035569081, "uncontrollable_anger", 20, 559, 0, 99, 69, 94.01f, 73.01481f, 113.521805f, 0f, 0.9778458f, 0f, 0.20932665f, (SimpleCharFullUpdateFlags)36325955, 0, "00000000000000008000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-222339", "2026-07-10T03:30:11.4340964Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035569101, "uncontrollable_anger", 23, 658, 0, 100, 80, 171.61719f, 73.01637f, 95.93813f, 0f, 0.41580284f, 0f, 0.9094548f, (SimpleCharFullUpdateFlags)36325955, 0, "00000000000000000000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-222339", "2026-07-10T03:28:28.9690500Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035569102, "uncontrollable_anger", 23, 658, 0, 100, 80, 171.55113f, 73.01637f, 102.018234f, 0f, -0.913844f, 0f, -0.4060654f, (SimpleCharFullUpdateFlags)36325955, 0, "00000000000000008000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-222339", "2026-07-10T03:28:28.9690500Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035569154, "uncontrollable_anger", 19, 526, 0, 98, 66, 241.53693f, 79.01797f, 196.73727f, 0f, 0.8662456f, 0f, -0.49961853f, (SimpleCharFullUpdateFlags)36325955, 0, "80000000000000008000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-222339", "2026-07-10T03:24:04.2684007Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035569170, "uncontrollable_anger", 13, 327, 0, 96, 45, 313.01f, 102.81483f, 177.43071f, 0f, 0.99989957f, 0f, -0.014174349f, (SimpleCharFullUpdateFlags)36391491, 0, "BD2E27BCBBBBBAE2BFBFEBC602020101000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[2]
		{
			new CapturedSubwayWaypointDefinition(313.01f, 102.81483f, 177.43071f),
			new CapturedSubwayWaypointDefinition(312.9f, 102.8f, 173.90001f)
		}, "", "20260709-222339", "2026-07-10T03:24:00.0648983Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035525711, "workman_striker", 17, 459, 0, 98, 59, 323.12003f, 102.8164f, 188.04051f, 0f, -0.023697615f, 0f, 0.99971914f, (SimpleCharFullUpdateFlags)34294475, 0, "BD918EA53A9189AA3FBFC8C702020101000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[2]
		{
			new CapturedSubwayWaypointDefinition(323.12003f, 102.8164f, 188.04051f),
			new CapturedSubwayWaypointDefinition(323.0508f, 102.8164f, 189.49991f)
		}, "", "20260709-212336", "2026-07-10T02:33:50.3931237Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035526128, "workman_striker", 15, 393, 0, 97, 52, 334.03513f, 106.8164f, 180.3174f, 0f, 0.038731117f, 0f, 0.9992497f, (SimpleCharFullUpdateFlags)34228939, 0, "00000000000000000000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-212336", "2026-07-10T02:33:47.2707446Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035526157, "workman_striker", 15, 393, 0, 97, 52, 345.64084f, 102.81483f, 166.29303f, 0f, 0.33765343f, 0f, 0.94127053f, (SimpleCharFullUpdateFlags)34294475, 0, "3F6DCA75368034943F90656E02020101000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[2]
		{
			new CapturedSubwayWaypointDefinition(345.64084f, 102.81483f, 166.29303f),
			new CapturedSubwayWaypointDefinition(346.9706f, 102.81483f, 167.92276f)
		}, "", "20260709-212336", "2026-07-10T02:26:37.6139282Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035526166, "workman_striker", 14, 360, 0, 97, 48, 322.94794f, 102.8164f, 244.02061f, 0f, -0.007538848f, 0f, 0.99997157f, (SimpleCharFullUpdateFlags)34294475, 0, "BCB94477BBE490DC3FBFF9E002020101000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[2]
		{
			new CapturedSubwayWaypointDefinition(322.94794f, 102.8164f, 244.02061f),
			new CapturedSubwayWaypointDefinition(322.9f, 102.8f, 247.2f)
		}, "", "20260709-212336", "2026-07-10T02:26:52.9472889Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035526263, "workman_striker", 15, 393, 0, 97, 52, 300.79254f, 102.8164f, 249.23615f, 0f, -0.6521332f, 0f, 0.7581044f, (SimpleCharFullUpdateFlags)34228939, 0, "80000000000000000000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-212336", "2026-07-10T02:26:37.6149272Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035526334, "workman_striker", 14, 360, 0, 97, 48, 325.2369f, 102.8164f, 254.85397f, 0f, -0.20742477f, 0f, 0.97825074f, (SimpleCharFullUpdateFlags)34228939, 0, "80000000000000000000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-212336", "2026-07-10T02:26:37.6149272Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035526377, "workman_striker", 14, 360, 0, 97, 48, 346.8438f, 102.8164f, 245.3138f, 0f, -0.5409632f, 0f, 0.84104633f, (SimpleCharFullUpdateFlags)34228939, 0, "80000000000000000000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-212336", "2026-07-10T02:26:37.6149272Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035526403, "workman_striker", 14, 360, 0, 97, 48, 290.87592f, 102.8164f, 253.12694f, 0f, -0.8381587f, 0f, 0.54542613f, (SimpleCharFullUpdateFlags)34228939, 0, "80000000000000008000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-212336", "2026-07-10T02:26:37.6149272Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035527573, "workman_striker", 15, 393, 0, 97, 52, 335.81003f, 102.8164f, 270.74283f, 0f, -0.99970436f, 0f, 0.0243126f, (SimpleCharFullUpdateFlags)34228939, 0, "80000000000000008000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-212336", "2026-07-10T02:31:28.0840211Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035527608, "workman_striker", 14, 360, 0, 97, 48, 319.8084f, 102.8164f, 275.9862f, 0f, -0.6883934f, 0f, -0.7253375f, (SimpleCharFullUpdateFlags)34228939, 0, "00000000000000000000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-212336", "2026-07-10T02:31:28.0840211Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035527612, "workman_striker", 16, 426, 0, 97, 55, 349.09937f, 102.8164f, 266.80792f, 0f, 0.3353121f, 0f, 0.9421071f, (SimpleCharFullUpdateFlags)34228939, 0, "00000000000000000000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-212336", "2026-07-10T02:33:47.2697443Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035527645, "workman_striker", 16, 426, 0, 97, 55, 353.12708f, 102.8164f, 273.67963f, 0f, -0.27288726f, 0f, 0.96204597f, (SimpleCharFullUpdateFlags)34228939, 0, "80000000000000000000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-212336", "2026-07-10T02:33:47.2697443Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035527673, "workman_striker", 14, 360, 0, 97, 48, 331.62442f, 106.715f, 276.6042f, 0f, -0.9746764f, 0f, -0.22361977f, (SimpleCharFullUpdateFlags)34228939, 0, "00000000000000008000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-212336", "2026-07-10T02:31:28.0840211Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035568640, "workman_striker", 16, 426, 0, 97, 55, 327.76028f, 102.8164f, 262.487f, 0f, -0.7806542f, 0f, 0.62496316f, (SimpleCharFullUpdateFlags)34228939, 0, "80000000000000008000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-212336", "2026-07-10T02:31:28.0840211Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035568666, "workman_striker", 14, 360, 0, 97, 48, 337.41483f, 102.8164f, 270.85977f, 0f, -0.9583983f, 0f, 0.28543428f, (SimpleCharFullUpdateFlags)34294475, 0, "80000000000000008000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[1]
		{
			new CapturedSubwayWaypointDefinition(337.41483f, 102.8164f, 270.85977f)
		}, "", "20260709-212336", "2026-07-10T02:31:28.0840211Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035568904, "workman_striker", 13, 327, 0, 96, 45, 322.7869f, 102.815f, 214.8705f, 0f, -0.7168956f, 0f, 0.69718057f, (SimpleCharFullUpdateFlags)34228939, 0, "80000000000000008000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-212336", "2026-07-10T02:35:57.6643809Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035569098, "workman_striker", 25, 724, 0, 100, 86, 122.3958f, 73.01637f, 85.70895f, 0f, 0.57596123f, 0f, 0.8174769f, (SimpleCharFullUpdateFlags)34228939, 0, "00000000000000000000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-222339", "2026-07-10T03:29:20.8121828Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035569157, "workman_striker", 13, 327, 0, 96, 45, 336.8544f, 106.715f, 154.08716f, 0f, -0.9996315f, 0f, 0.027144851f, (SimpleCharFullUpdateFlags)34294475, 0, "80000000000000008000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[1]
		{
			new CapturedSubwayWaypointDefinition(336.8544f, 106.715f, 154.08716f)
		}, "", "20260709-222339", "2026-07-10T03:24:04.2322101Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035569171, "workman_striker", 17, 459, 0, 98, 59, 347.47372f, 102.81483f, 179.65518f, 0f, 0.4417334f, 0f, 0.89714634f, (SimpleCharFullUpdateFlags)34294475, 0, "00000000000000000000000002010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[1]
		{
			new CapturedSubwayWaypointDefinition(347.47372f, 102.81483f, 179.65518f)
		}, "", "20260709-222339", "2026-07-10T03:24:00.0648983Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035569177, "workman_striker", 16, 426, 0, 97, 55, 323.33896f, 102.81483f, 181.06703f, 0f, 0.0012651333f, 0f, 0.99999917f, (SimpleCharFullUpdateFlags)34228939, 0, "00000000000000000000000003010001000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[0], "", "20260709-222339", "2026-07-10T03:24:00.0648983Z"),
		new CapturedSubwayOrdinarySpawnDefinition(2035569188, "workman_striker", 16, 426, 0, 97, 55, 311.36737f, 102.81483f, 180.92172f, 0f, 0.09010437f, 0f, 0.99593234f, (SimpleCharFullUpdateFlags)34294475, 0, "3E89A410388B7AB03FBC9CF702020101000100010001000000020000", 0, new CapturedSubwayWaypointDefinition[2]
		{
			new CapturedSubwayWaypointDefinition(311.36737f, 102.81483f, 180.92172f),
			new CapturedSubwayWaypointDefinition(312.16846f, 102.81483f, 184.85887f)
		}, "", "20260709-222339", "2026-07-10T03:24:00.0648983Z")
	};

	private static readonly Dictionary<string, CapturedSubwayOrdinaryArchetypeDefinition> ArchetypesByKey = Archetypes.ToDictionary((CapturedSubwayOrdinaryArchetypeDefinition value) => value.Key, StringComparer.Ordinal);

	public CapturedSubwayOrdinaryArchetypeDefinition[] GetArchetypes()
	{
		return Archetypes.ToArray();
	}

	public CapturedSubwayCorpseEvidenceDefinition[] GetCorpseEvidence(int monsterData)
	{
		return (from value in SupportedCorpseEvidence.Concat(Archetypes.SelectMany((CapturedSubwayOrdinaryArchetypeDefinition value) => value.CorpseEvidence))
			where value.MonsterData == monsterData
			select value).ToArray();
	}

	public CapturedSubwayLootOutcomeEvidenceDefinition[] GetLootOutcomeEvidence(int monsterData)
	{
		return (from value in SupportedLootOutcomeEvidence.Concat(Archetypes.SelectMany((CapturedSubwayOrdinaryArchetypeDefinition value) => value.LootOutcomeEvidence))
			where value.MonsterData == monsterData
			select value).ToArray();
	}

	public CapturedSubwaySourceWeaponEvidenceDefinition[] GetSourceWeaponEvidence(int monsterData)
	{
		CapturedSubwaySourceWeaponProfileDefinition capturedSubwaySourceWeaponProfileDefinition = SupportedSourceWeaponProfiles.SingleOrDefault((CapturedSubwaySourceWeaponProfileDefinition value) => value.MonsterData == monsterData);
		if (capturedSubwaySourceWeaponProfileDefinition != null)
		{
			return capturedSubwaySourceWeaponProfileDefinition.SourceWeaponEvidence.ToArray();
		}
		CapturedSubwayOrdinaryArchetypeDefinition capturedSubwayOrdinaryArchetypeDefinition = Archetypes.SingleOrDefault((CapturedSubwayOrdinaryArchetypeDefinition value) => value.MonsterData == monsterData);
		return (capturedSubwayOrdinaryArchetypeDefinition == null) ? new CapturedSubwaySourceWeaponEvidenceDefinition[0] : capturedSubwayOrdinaryArchetypeDefinition.SourceWeaponEvidence.ToArray();
	}

	public CapturedSubwayGenerationVariantDefinition[] GetGenerationVariants(int monsterData, int sourceInstance)
	{
		return GenerationVariants.Where((CapturedSubwayGenerationVariantDefinition value) => value.MonsterData == monsterData && value.SourceInstance == sourceInstance).ToArray();
	}

	public CapturedSubwayStrictLootProfileDefinition GetStrictLootProfile(int monsterData)
	{
		return StrictLootProfiles.SingleOrDefault((CapturedSubwayStrictLootProfileDefinition value) => value.MonsterData == monsterData);
	}

	public CapturedSubwayOrdinarySpawnDefinition[] GetSpawns()
	{
		return Spawns.ToArray();
	}

	internal CapturedSubwayOrdinarySpawnDefinition[] GetAllSpawns()
	{
		return Spawns.ToArray();
	}

	public bool TryGetArchetype(string key, out CapturedSubwayOrdinaryArchetypeDefinition archetype)
	{
		return ArchetypesByKey.TryGetValue(key, out archetype);
	}

	public CombatLootTableEntry[] BuildCapturedLootEntries()
	{
		List<CombatLootTableEntry> list = new List<CombatLootTableEntry>();
		CapturedSubwayStrictLootProfileDefinition[] strictLootProfiles = StrictLootProfiles;
		foreach (CapturedSubwayStrictLootProfileDefinition capturedSubwayStrictLootProfileDefinition in strictLootProfiles)
		{
			AddCapturedLootEntries(list, capturedSubwayStrictLootProfileDefinition.Name, capturedSubwayStrictLootProfileDefinition.MonsterData, 0, capturedSubwayStrictLootProfileDefinition.Entries);
		}
		foreach (CapturedSubwayOrdinaryArchetypeDefinition item in Archetypes.Where((CapturedSubwayOrdinaryArchetypeDefinition value) => StrictLootProfiles.All((CapturedSubwayStrictLootProfileDefinition strictLoot) => strictLoot.MonsterData != value.MonsterData)))
		{
			AddCapturedLootEntries(list, item.Name, item.MonsterData, item.NpcFamily, item.LootEvidence);
		}
		return list.ToArray();
	}

	private static void AddCapturedLootEntries(List<CombatLootTableEntry> entries, string name, int monsterData, int npcFamily, CapturedSubwayLootEvidenceDefinition[] lootEvidence)
	{
		int num = 0;
		foreach (CapturedSubwayLootEvidenceDefinition capturedSubwayLootEvidenceDefinition in lootEvidence)
		{
			entries.Add(new CombatLootTableEntry
			{
				ExactName = name,
				MonsterData = monsterData,
				NpcFamily = npcFamily,
				Slot = num++,
				DropChanceBasisPoints = capturedSubwayLootEvidenceDefinition.ObservedBasisPoints,
				ItemTemplates = new CombatLootItemTemplate[1]
				{
					new CombatLootItemTemplate
					{
						LowId = capturedSubwayLootEvidenceDefinition.LowId,
						HighId = capturedSubwayLootEvidenceDefinition.HighId,
						MinQuality = capturedSubwayLootEvidenceDefinition.Quality,
						MaxQuality = capturedSubwayLootEvidenceDefinition.Quality,
						RangeCheck = 0,
						DropGroupHash = "captured-subway-ordinary"
					}
				}
			});
		}
	}
}
