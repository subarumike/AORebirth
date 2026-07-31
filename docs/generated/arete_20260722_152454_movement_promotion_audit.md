# Movement Promotion Audit — 20260722-152454

Generated deterministically by `tools-temp/AOSharpCaptureAnalyzer/audit_movement_promotion_candidates.py`. This report is analysis-only; it does not modify runtime data.

## Verdict

- Captured usable `FollowTarget/NpcPath` rows audited: **9,526 / 9,526**.
- Canonical route groups after removing runtime identity/generation from the key: **1,440**.
- Safe for immediate promotion: **0 routes / 0 paths**.
- Requires live verification: **69 routes / 660 paths**.
- Reject: **1,371 routes / 8,866 paths**.

Every captured path is accounted for exactly once. Runtime identities and respawn generations remain evidence fields only; the canonical key is `(NPC family, MonsterData template, level, playfield, route signature)`.

## Movement classification

| Classification | Path rows |
| --- | ---: |
| idle | 0 |
| patrol | 7,771 |
| combat chase | 360 |
| flee | 34 |
| leash | 152 |
| spawn | 614 |
| scripted | 595 |

## Safe for immediate promotion

None.

## Requires live verification

| Score | Classification | Family | Template | Level | PF | Names | Signature | Paths | IDs | Generations | Edges | Closed | Decision |
| ---: | --- | ---: | ---: | ---: | ---: | --- | --- | ---: | ---: | ---: | ---: | --- | --- |
| 70 | patrol | 25 | 17657 | 1 | 1044525 | Garbage Flea | 329b9e516a728209 | 6 | 1 | 1 | 4 | yes | route_not_repeated_end_to_end, single_identity_generation_support; path=(3502.5,5.0,902.0) -> (3501.5,6.0,908.0) -> (3502.5,5.0,903.5) -> (3503.0,5.0,896.0) -> (3502.5,5.0,889.0) -> (3503.0,5.0,896.0) -> (3502.5,5.0,903.5) |
| 70 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 4356708d71283118 | 8 | 1 | 1 | 3 | yes | route_not_repeated_end_to_end, single_identity_generation_support; path=(3597.5,51.5,773.0) -> (3597.0,52.5,772.0) -> (3597.5,52.5,772.0) -> (3597.0,52.5,772.0) -> (3597.5,52.5,772.0) -> (3597.0,52.5,772.5) -> (3597.5,52.5,772.0) |
| 70 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | bdaa5dbcc1231ffa | 4 | 1 | 1 | 3 | yes | route_not_repeated_end_to_end, single_identity_generation_support; path=(3602.5,52.0,787.0) -> (3602.0,52.5,787.5) -> (3602.0,52.5,788.0) -> (3602.5,52.5,788.0) |
| 70 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | f39ea696140053f8 | 7 | 1 | 1 | 4 | yes | route_not_repeated_end_to_end, single_identity_generation_support; path=(3600.0,52.0,787.5) -> (3599.5,52.5,787.0) -> (3599.0,52.5,786.5) -> (3599.0,52.5,787.0) -> (3599.0,52.5,787.5) |
| 65 | patrol | 25 | 17657 | 5 | 1044525 | Garbage Flea | 44fa0efbf8c94940 | 92 | 2 | 2 | 7 | no | open_route_not_closed, branched_route_requires_live_confirmation, route_not_repeated_end_to_end; path=(3451.5,0.0,853.5) -> (3454.0,1.0,846.5) -> (3454.5,1.0,841.5) -> (3452.0,1.5,865.0) -> (3450.5,1.0,862.0) -> (3451.0,1.0,858.0) -> (3451.5,1.0,852.5) -> (3454.0,1.0,846.5) -> … (+41) |
| 65 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 2b43550b24c8bbda | 5 | 1 | 1 | 2 | yes | route_not_repeated_end_to_end, single_identity_generation_support, insufficient_route_geometry; path=(3603.0,52.0,789.0) -> (3602.0,52.5,788.0) -> (3602.5,52.5,788.0) -> (3602.0,52.5,788.0) |
| 65 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 568bc6979cd6f9a6 | 3 | 1 | 1 | 2 | yes | route_not_repeated_end_to_end, single_identity_generation_support, insufficient_route_geometry; path=(3622.0,51.5,798.5) -> (3622.5,52.5,799.0) -> (3623.0,52.5,798.5) |
| 65 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 609bc29ce3962461 | 5 | 1 | 1 | 2 | yes | route_not_repeated_end_to_end, single_identity_generation_support, insufficient_route_geometry; path=(3613.0,52.0,788.0) -> (3612.0,52.5,787.5) -> (3612.0,52.5,788.5) -> (3612.0,52.5,787.5) |
| 65 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 64654d6f03ff8e7a | 1 | 1 | 1 | 1 | yes | route_not_repeated_end_to_end, single_identity_generation_support, insufficient_route_geometry; path=(3622.5,51.5,798.0) -> (3623.5,52.5,799.0) |
| 55 | patrol | 0 | 26092 | 40 | 1044525 | ICC Peacekeeper | dca239d0df03140f | 11 | 1 | 1 | 11 | no | open_route_not_closed, route_not_repeated_end_to_end, single_identity_generation_support; path=(3412.0,4.0,773.0) -> (3410.0,4.5,779.0) -> (3413.0,5.0,782.0) -> (3417.0,5.0,782.0) -> (3421.5,5.0,781.0) -> (3422.5,5.0,781.0) -> (3427.0,5.0,781.0) -> (3435.5,5.0,781.0) -> … (+4) |
| 55 | patrol | 0 | 26092 | 40 | 1044525 | ICC Peacekeeper | f2896b7a63da1851 | 8 | 1 | 1 | 8 | no | open_route_not_closed, route_not_repeated_end_to_end, single_identity_generation_support; path=(3469.5,4.0,781.5) -> (3465.0,6.0,782.5) -> (3460.5,4.5,782.0) -> (3453.5,5.0,782.0) -> (3453.5,5.0,786.0) -> (3456.5,5.0,786.5) -> (3464.0,8.0,787.0) -> (3473.0,8.0,786.5) -> … (+1) |
| 55 | patrol | 15 | 17662 | 1 | 1044525 | Minibronto | 3295a10b611beba1 | 4 | 1 | 1 | 4 | no | open_route_not_closed, route_not_repeated_end_to_end, single_identity_generation_support; path=(3508.0,8.5,444.0) -> (3505.5,8.0,445.0) -> (3500.0,8.0,446.5) -> (3497.5,8.0,450.0) -> (3501.0,8.0,451.0) |
| 55 | patrol | 15 | 17662 | 1 | 1044525 | Minibronto | 7524a21de343172e | 10 | 1 | 1 | 10 | no | open_route_not_closed, route_not_repeated_end_to_end, single_identity_generation_support; path=(3498.5,8.0,449.5) -> (3502.5,8.0,449.0) -> (3506.5,8.0,447.5) -> (3509.5,8.5,446.0) -> (3507.0,8.5,442.0) -> (3501.0,8.0,444.0) -> (3499.0,8.0,445.5) -> (3504.5,8.0,446.5) -> … (+3) |
| 55 | patrol | 25 | 17657 | 5 | 1044525 | Garbage Flea | 2076e24038a5f5d3 | 7 | 1 | 1 | 7 | no | open_route_not_closed, route_not_repeated_end_to_end, single_identity_generation_support; path=(3424.0,0.0,861.5) -> (3422.5,0.0,875.5) -> (3422.0,0.0,891.0) -> (3426.0,0.0,889.0) -> (3426.5,0.0,882.0) -> (3426.0,0.0,860.5) -> (3426.5,0.0,854.0) -> (3427.0,0.0,830.0) |
| 55 | patrol | 25 | 17657 | 5 | 1044525 | Garbage Flea | 46ac65bcd1d596d1 | 3 | 1 | 1 | 3 | no | open_route_not_closed, route_not_repeated_end_to_end, single_identity_generation_support; path=(3451.5,0.0,863.5) -> (3450.5,1.0,862.0) -> (3451.0,1.0,858.0) -> (3451.5,1.0,852.5) |
| 55 | patrol | 25 | 17657 | 5 | 1044525 | Garbage Flea | 912176e5c85b350a | 7 | 1 | 1 | 7 | no | open_route_not_closed, route_not_repeated_end_to_end, single_identity_generation_support; path=(3423.5,0.0,871.5) -> (3423.5,0.0,866.0) -> (3421.5,0.0,865.0) -> (3423.0,0.0,871.5) -> (3424.5,0.0,873.5) -> (3425.0,0.0,877.5) -> (3427.0,0.0,879.5) -> (3429.5,0.0,882.5) |
| 55 | patrol | 25 | 17657 | 5 | 1044525 | Garbage Flea | d91bc8c1eebb1a78 | 9 | 1 | 1 | 9 | no | open_route_not_closed, route_not_repeated_end_to_end, single_identity_generation_support; path=(3424.5,0.0,885.5) -> (3420.5,0.0,888.5) -> (3419.5,0.0,891.0) -> (3419.5,0.0,893.5) -> (3425.0,2.0,895.0) -> (3426.5,3.5,894.5) -> (3430.0,3.5,894.5) -> (3422.0,3.0,896.5) -> … (+2) |
| 55 | patrol | 25 | 17657 | 5 | 1044525 | Garbage Flea | fa0303c2d08ac4b2 | 3 | 1 | 1 | 3 | no | open_route_not_closed, route_not_repeated_end_to_end, single_identity_generation_support; path=(3423.0,0.0,874.5) -> (3422.0,0.0,891.0) -> (3426.0,0.0,889.0) -> (3426.5,0.0,882.0) |
| 55 | patrol | 25 | 17657 | 5 | 1044525 | Garbage Flea | fbae5a45df29f9f0 | 5 | 1 | 1 | 5 | no | open_route_not_closed, route_not_repeated_end_to_end, single_identity_generation_support; path=(3454.0,0.0,843.0) -> (3452.0,1.5,865.0) -> (3450.5,1.0,862.0) -> (3451.0,1.0,858.0) -> (3451.5,1.0,852.5) -> (3454.0,1.0,846.5) |
| 55 | patrol | 25 | 17657 | 6 | 1044525 | Garbage Flea | 1f56bcedf62eae88 | 5 | 1 | 1 | 5 | no | open_route_not_closed, route_not_repeated_end_to_end, single_identity_generation_support; path=(3426.5,0.0,855.5) -> (3427.0,0.0,830.0) -> (3426.5,0.0,810.5) -> (3426.5,0.0,806.5) -> (3430.0,0.0,806.0) -> (3441.0,0.0,806.0) |
| 55 | patrol | 25 | 17657 | 6 | 1044525 | Garbage Flea | 30443bc09db8f268 | 3 | 1 | 1 | 3 | no | open_route_not_closed, route_not_repeated_end_to_end, single_identity_generation_support; path=(3426.5,0.0,855.5) -> (3427.0,0.0,830.0) -> (3426.5,0.0,810.5) -> (3426.5,0.0,806.5) |
| 55 | patrol | 25 | 17657 | 6 | 1044525 | Garbage Flea | 70574543e3b03407 | 3 | 1 | 1 | 3 | no | open_route_not_closed, route_not_repeated_end_to_end, single_identity_generation_support; path=(3450.0,0.0,818.5) -> (3451.5,0.5,812.5) -> (3453.5,0.5,808.0) -> (3449.0,0.5,813.0) |
| 55 | patrol | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | 500bc11f4ddf89ea | 3 | 1 | 1 | 3 | no | open_route_not_closed, route_not_repeated_end_to_end, single_identity_generation_support; path=(3568.5,2.0,543.0) -> (3565.0,3.0,550.0) -> (3562.5,2.5,559.0) -> (3563.0,3.5,569.5) |
| 55 | patrol | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | 8ea9e698815173d0 | 11 | 1 | 1 | 11 | no | open_route_not_closed, route_not_repeated_end_to_end, single_identity_generation_support; path=(3438.5,12.5,821.0) -> (3437.5,13.0,819.0) -> (3438.5,12.0,815.5) -> (3448.5,9.5,818.0) -> (3451.0,9.5,826.5) -> (3447.5,9.5,836.0) -> (3447.5,9.5,845.5) -> (3440.5,9.5,856.0) -> … (+4) |
| 55 | patrol | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | 970cb1ad1a657171 | 12 | 1 | 1 | 12 | no | open_route_not_closed, route_not_repeated_end_to_end, single_identity_generation_support; path=(3452.0,4.0,761.5) -> (3454.5,4.5,753.5) -> (3454.5,4.5,751.5) -> (3449.5,4.5,749.0) -> (3446.0,5.0,745.0) -> (3440.0,3.5,741.0) -> (3432.0,3.5,735.0) -> (3428.0,3.0,727.5) -> … (+5) |
| 55 | patrol | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | a083371681f6a5ca | 7 | 1 | 1 | 7 | no | open_route_not_closed, route_not_repeated_end_to_end, single_identity_generation_support; path=(3525.0,2.0,701.5) -> (3523.5,2.5,699.0) -> (3520.0,2.5,700.0) -> (3515.0,2.5,699.0) -> (3508.0,2.5,697.5) -> (3501.5,2.5,695.5) -> (3499.0,2.5,695.0) -> (3496.5,2.5,692.0) |
| 55 | patrol | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | b97051b77d4edb4c | 4 | 1 | 1 | 4 | no | open_route_not_closed, route_not_repeated_end_to_end, single_identity_generation_support; path=(3595.5,5.0,871.0) -> (3571.0,5.5,866.0) -> (3562.5,6.5,865.0) -> (3552.5,5.5,864.0) -> (3544.0,6.0,866.5) |
| 55 | patrol | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | bf3959b8fac815cf | 17 | 1 | 1 | 17 | no | open_route_not_closed, route_not_repeated_end_to_end, single_identity_generation_support; path=(3673.0,3.5,417.0) -> (3665.5,6.5,425.5) -> (3661.0,5.5,432.5) -> (3655.5,3.5,439.0) -> (3650.5,3.0,446.0) -> (3646.0,1.0,454.5) -> (3639.5,0.5,461.0) -> (3632.0,0.5,466.0) -> … (+10) |
| 55 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 1abf7ce545f826d6 | 4 | 1 | 1 | 4 | no | open_route_not_closed, route_not_repeated_end_to_end, single_identity_generation_support; path=(3565.5,2.5,545.5) -> (3566.5,3.5,554.5) -> (3561.0,2.5,563.0) -> (3561.0,3.5,571.5) -> (3561.0,4.0,577.5) |
| 55 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 5ddda52763f503f5 | 6 | 1 | 1 | 6 | no | open_route_not_closed, route_not_repeated_end_to_end, single_identity_generation_support; path=(3453.5,4.0,755.0) -> (3455.5,4.5,752.0) -> (3454.0,4.0,750.0) -> (3448.0,4.5,748.5) -> (3443.5,4.0,744.5) -> (3438.0,3.5,737.0) -> (3431.5,3.5,730.0) |
| 55 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 612e040a4339bf85 | 3 | 1 | 1 | 3 | no | open_route_not_closed, route_not_repeated_end_to_end, single_identity_generation_support; path=(3596.0,5.0,871.5) -> (3564.5,5.5,865.0) -> (3556.0,5.5,864.5) -> (3546.5,6.0,868.5) |
| 55 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 6a6936692e85283d | 5 | 1 | 1 | 5 | no | open_route_not_closed, route_not_repeated_end_to_end, single_identity_generation_support; path=(3525.5,2.0,683.0) -> (3529.5,2.5,682.0) -> (3532.5,2.5,681.0) -> (3536.5,2.5,685.0) -> (3542.5,2.5,686.5) -> (3549.5,2.5,690.5) |
| 55 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | e036455ad6f7774e | 50 | 1 | 1 | 50 | no | open_route_not_closed, route_not_repeated_end_to_end, single_identity_generation_support; path=(3378.5,2.0,574.0) -> (3380.0,2.5,571.5) -> (3384.5,2.5,569.5) -> (3388.5,2.5,569.0) -> (3385.0,2.5,569.0) -> (3382.5,2.5,571.5) -> (3380.5,2.5,569.0) -> (3382.5,2.5,565.5) -> … (+43) |
| 55 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | 4ec3bc501e7eec0c | 10 | 1 | 1 | 10 | no | open_route_not_closed, route_not_repeated_end_to_end, single_identity_generation_support; path=(3412.0,2.0,689.0) -> (3414.5,3.0,690.5) -> (3419.5,3.5,687.5) -> (3422.0,3.0,679.0) -> (3428.0,3.0,672.0) -> (3430.5,3.0,675.0) -> (3434.5,3.0,676.5) -> (3442.5,2.5,676.5) -> … (+3) |
| 55 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | a0ec2965024b94c9 | 3 | 1 | 1 | 3 | no | open_route_not_closed, route_not_repeated_end_to_end, single_identity_generation_support; path=(3595.5,5.0,871.5) -> (3570.5,5.5,864.0) -> (3561.5,5.5,865.5) -> (3552.5,5.5,865.5) |
| 55 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | abb9675d11c96678 | 3 | 1 | 1 | 3 | no | open_route_not_closed, route_not_repeated_end_to_end, single_identity_generation_support; path=(3566.0,2.0,545.0) -> (3563.5,3.5,554.0) -> (3561.0,2.5,563.0) -> (3562.5,4.0,574.0) |
| 55 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | acae2553fd86fa78 | 4 | 1 | 1 | 4 | no | open_route_not_closed, route_not_repeated_end_to_end, single_identity_generation_support; path=(3566.5,0.0,404.0) -> (3569.5,0.5,404.0) -> (3575.5,0.5,403.0) -> (3581.5,0.5,406.0) -> (3588.5,0.5,406.5) |
| 55 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | aeddb75ce08a1bf6 | 7 | 1 | 1 | 7 | no | open_route_not_closed, route_not_repeated_end_to_end, single_identity_generation_support; path=(3525.0,2.0,699.5) -> (3522.5,2.5,699.0) -> (3519.5,2.5,697.5) -> (3513.0,2.5,698.5) -> (3504.5,2.5,693.0) -> (3502.5,2.5,692.5) -> (3500.0,2.5,694.5) -> (3496.5,2.5,692.0) |
| 55 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | cdf41e7adb2a83a4 | 6 | 1 | 1 | 6 | no | open_route_not_closed, route_not_repeated_end_to_end, single_identity_generation_support; path=(3454.5,4.0,756.0) -> (3455.5,4.5,753.0) -> (3455.5,4.5,749.5) -> (3449.5,4.5,748.5) -> (3444.0,4.0,744.0) -> (3435.5,3.5,739.0) -> (3431.0,3.5,730.0) |
| 55 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | d02a8bc5a5635854 | 5 | 1 | 1 | 5 | no | open_route_not_closed, route_not_repeated_end_to_end, single_identity_generation_support; path=(3603.0,0.0,492.5) -> (3593.5,0.5,503.5) -> (3587.0,1.0,509.5) -> (3581.0,1.0,516.5) -> (3572.5,2.5,521.5) -> (3567.5,2.5,527.5) |
| 55 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 1f8150f7c468b287 | 4 | 1 | 1 | 4 | no | open_route_not_closed, route_not_repeated_end_to_end, single_identity_generation_support; path=(3595.0,51.5,799.5) -> (3594.0,52.5,800.0) -> (3601.5,52.5,788.0) -> (3602.0,52.5,788.0) -> (3622.5,52.5,799.0) |
| 55 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 2603228cbaa5cacf | 3 | 1 | 1 | 3 | no | open_route_not_closed, route_not_repeated_end_to_end, single_identity_generation_support; path=(3613.0,52.0,787.5) -> (3612.0,52.5,788.0) -> (3612.0,52.5,788.5) -> (3594.5,52.5,800.0) |
| 55 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 94f8b30eb1565781 | 7 | 1 | 1 | 6 | no | open_route_not_closed, route_not_repeated_end_to_end, single_identity_generation_support; path=(3598.5,51.5,788.0) -> (3598.5,52.5,787.0) -> (3599.0,52.5,787.5) -> (3599.5,52.5,787.0) -> (3601.5,52.5,788.0) -> (3602.0,52.5,787.5) -> (3602.0,52.5,788.0) |
| 55 | patrol | 1019 | 297023 | 1 | 1044525 | Cleaning Robot | a9a7f723973944a7 | 7 | 1 | 1 | 7 | no | open_route_not_closed, route_not_repeated_end_to_end, single_identity_generation_support; path=(3591.0,25.5,879.0) -> (3589.0,25.0,882.0) -> (3589.5,24.0,885.5) -> (3589.5,23.0,890.0) -> (3590.0,20.5,896.0) -> (3590.0,18.5,901.5) -> (3593.5,18.5,902.0) -> (3593.0,20.5,896.5) |
| 50 | patrol | 0 | 26092 | 40 | 1044525 | ICC Peacekeeper | 66fd8ce36aef4e55 | 2 | 1 | 1 | 2 | no | open_route_not_closed, route_not_repeated_end_to_end, single_identity_generation_support, insufficient_route_geometry; path=(3406.5,9.0,806.5) -> (3400.0,9.0,806.0) -> (3396.5,11.0,805.5) |
| 50 | patrol | 25 | 17657 | 5 | 1044525 | Garbage Flea | 79b9c13c2a814ade | 1 | 1 | 1 | 1 | no | open_route_not_closed, route_not_repeated_end_to_end, single_identity_generation_support, insufficient_route_geometry; path=(3449.0,0.0,826.0) -> (3449.0,0.5,833.0) |
| 50 | patrol | 58 | 17712 | 13 | 1044525 | Saltworm | eb6cc49726cd4a49 | 2 | 1 | 1 | 2 | no | open_route_not_closed, route_not_repeated_end_to_end, single_identity_generation_support, insufficient_route_geometry; path=(3499.0,0.0,627.5) -> (3511.0,0.0,613.5) -> (3534.0,0.0,596.5) |
| 50 | patrol | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | 1ea14dd911328029 | 2 | 1 | 1 | 2 | no | open_route_not_closed, route_not_repeated_end_to_end, single_identity_generation_support, insufficient_route_geometry; path=(3603.0,0.0,403.5) -> (3604.0,0.5,401.0) -> (3604.5,0.5,398.0) |
| 50 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 58052c71a68bad13 | 1 | 1 | 1 | 1 | no | open_route_not_closed, route_not_repeated_end_to_end, single_identity_generation_support, insufficient_route_geometry; path=(3604.0,0.0,402.0) -> (3604.5,0.5,399.5) |
| 50 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | bf6c291721a3703a | 2 | 1 | 1 | 2 | no | open_route_not_closed, route_not_repeated_end_to_end, single_identity_generation_support, insufficient_route_geometry; path=(3522.5,2.0,701.5) -> (3519.0,2.5,700.0) -> (3516.5,2.5,696.0) |
| 50 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | f1236d240b6b750e | 1 | 1 | 1 | 1 | no | open_route_not_closed, route_not_repeated_end_to_end, single_identity_generation_support, insufficient_route_geometry; path=(3575.0,1.5,522.0) -> (3570.5,2.5,529.0) |
| 50 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | 61be404c2d421323 | 1 | 1 | 1 | 1 | no | open_route_not_closed, route_not_repeated_end_to_end, single_identity_generation_support, insufficient_route_geometry; path=(3458.5,9.0,882.5) -> (3461.0,9.5,884.5) |
| 50 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | 921dcaca81be6990 | 1 | 1 | 1 | 1 | no | open_route_not_closed, route_not_repeated_end_to_end, single_identity_generation_support, insufficient_route_geometry; path=(3604.0,0.0,402.0) -> (3602.5,0.5,399.0) |
| 50 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 1f8bc0c79ae898b0 | 2 | 1 | 1 | 2 | no | open_route_not_closed, route_not_repeated_end_to_end, single_identity_generation_support, insufficient_route_geometry; path=(3600.0,52.0,787.5) -> (3599.0,52.5,787.5) -> (3623.0,52.5,799.5) |
| 40 | patrol | 25 | 17657 | 1 | 1044525 | Garbage Flea | 32d360dc4667e247 | 15 | 1 | 1 | 9 | no | open_route_not_closed, branched_route_requires_live_confirmation, route_not_repeated_end_to_end, single_identity_generation_support; path=(3546.5,7.0,887.5) -> (3541.0,8.5,888.0) -> (3541.5,7.0,895.5) -> (3549.0,7.0,898.0) -> (3555.5,8.0,902.0) -> (3555.5,8.0,906.5) -> (3555.5,8.0,910.5) -> (3555.5,8.0,906.5) -> … (+8) |
| 40 | patrol | 25 | 17657 | 1 | 1044525 | Garbage Flea | 8f7cc256246de906 | 12 | 1 | 1 | 6 | no | open_route_not_closed, branched_route_requires_live_confirmation, route_not_repeated_end_to_end, single_identity_generation_support; path=(3520.0,5.0,890.0) -> (3523.0,5.0,888.0) -> (3529.5,5.0,890.5) -> (3530.5,5.0,897.5) -> (3531.0,5.5,902.0) -> (3532.5,8.0,907.5) -> (3531.0,5.5,902.0) -> (3530.5,5.0,897.5) -> … (+5) |
| 40 | patrol | 25 | 17657 | 5 | 1044525 | Garbage Flea | 8a49a9958f6122ed | 34 | 1 | 1 | 6 | no | open_route_not_closed, branched_route_requires_live_confirmation, route_not_repeated_end_to_end, single_identity_generation_support; path=(3453.5,0.0,878.5) -> (3453.0,0.5,886.0) -> (3450.0,0.5,890.0) -> (3448.5,0.5,895.0) -> (3454.0,1.0,874.0) -> (3453.5,0.5,879.5) -> (3453.0,0.5,886.0) -> (3450.0,0.5,890.0) -> … (+27) |
| 40 | patrol | 25 | 17657 | 6 | 1044525 | Garbage Flea | 0bcebd7dbe77e80a | 6 | 1 | 1 | 6 | no | open_route_not_closed, branched_route_requires_live_confirmation, route_not_repeated_end_to_end, single_identity_generation_support; path=(3453.5,0.0,885.0) -> (3450.0,0.5,890.0) -> (3448.5,0.5,895.0) -> (3454.0,1.0,874.0) -> (3453.5,0.5,879.5) -> (3453.0,0.5,886.0) -> (3450.0,0.5,890.0) |
| 40 | patrol | 25 | 17657 | 6 | 1044525 | Garbage Flea | 44fa0efbf8c94940 | 45 | 1 | 1 | 7 | no | open_route_not_closed, branched_route_requires_live_confirmation, route_not_repeated_end_to_end, single_identity_generation_support; path=(3451.5,0.0,853.5) -> (3454.0,1.0,846.5) -> (3454.5,1.0,841.5) -> (3452.0,1.5,865.0) -> (3450.5,1.0,862.0) -> (3451.0,1.0,858.0) -> (3451.5,1.0,852.5) -> (3454.0,1.0,846.5) -> … (+38) |
| 40 | patrol | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | 76b0aed1c9677cd6 | 48 | 1 | 1 | 47 | no | open_route_not_closed, branched_route_requires_live_confirmation, route_not_repeated_end_to_end, single_identity_generation_support; path=(3379.0,2.0,574.5) -> (3382.0,2.5,573.5) -> (3384.5,2.5,570.0) -> (3388.5,2.5,570.5) -> (3384.5,2.5,570.0) -> (3380.5,2.5,569.0) -> (3386.0,2.5,567.5) -> (3388.5,2.5,562.5) -> … (+41) |
| 40 | scripted | 103 | 203740 | 2 | 1044525 | Protester | 4d7768dd4e68363d | 3 | 1 | 1 | 3 | no | scripted_semantics_require_live_confirmation, open_route_not_closed, route_not_repeated_end_to_end, single_identity_generation_support; path=(3509.0,5.0,792.5) -> (3512.5,5.0,794.5) -> (3517.5,5.0,797.5) -> (3526.5,5.0,798.5) |
| 40 | scripted | 137 | 30365 | 10 | 1044525 | Lolly the Reet | 44b6cc9c01f4bee2 | 6 | 1 | 1 | 6 | no | scripted_semantics_require_live_confirmation, open_route_not_closed, route_not_repeated_end_to_end, single_identity_generation_support; path=(3358.5,3.5,639.5) -> (3357.0,3.0,667.5) -> (3358.5,3.0,690.0) -> (3353.0,6.5,707.5) -> (3347.0,8.5,707.5) -> (3325.0,2.5,718.5) -> (3308.5,1.0,718.0) |
| 40 | scripted | 137 | 30365 | 10 | 1044525 | Lolly the Reet | 560180e051f79e41 | 11 | 1 | 1 | 11 | no | scripted_semantics_require_live_confirmation, open_route_not_closed, route_not_repeated_end_to_end, single_identity_generation_support; path=(3345.5,3.0,606.5) -> (3354.5,2.0,594.0) -> (3362.0,2.0,584.0) -> (3370.5,2.5,566.0) -> (3379.5,3.0,555.0) -> (3390.5,2.0,555.0) -> (3394.0,2.0,564.0) -> (3389.0,2.0,574.5) -> … (+4) |
| 40 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 04e4bd192af1642f | 4 | 1 | 1 | 3 | no | open_route_not_closed, branched_route_requires_live_confirmation, route_not_repeated_end_to_end, single_identity_generation_support; path=(3620.5,51.5,785.0) -> (3621.0,52.5,784.0) -> (3621.0,52.5,784.5) -> (3621.0,52.5,784.0) -> (3612.0,52.5,788.0) |
| 40 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 1745543a3f2b0f59 | 11 | 1 | 1 | 8 | no | open_route_not_closed, branched_route_requires_live_confirmation, route_not_repeated_end_to_end, single_identity_generation_support; path=(3620.5,51.5,785.0) -> (3621.0,52.5,784.0) -> (3621.0,52.5,784.5) -> (3620.5,52.5,784.0) -> (3621.0,52.5,784.5) -> (3621.0,52.5,784.0) -> (3620.5,52.5,784.0) -> (3612.5,52.5,788.0) -> … (+3) |
| 40 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 3015f63b3e032bc3 | 13 | 1 | 1 | 5 | no | open_route_not_closed, branched_route_requires_live_confirmation, route_not_repeated_end_to_end, single_identity_generation_support; path=(3599.5,51.5,786.0) -> (3597.0,52.5,772.0) -> (3596.5,52.5,772.0) -> (3597.5,52.5,772.0) -> (3597.0,52.5,772.0) -> (3597.0,52.5,772.5) -> (3597.0,52.5,772.0) -> (3597.5,52.5,772.0) -> … (+2) |
| 35 | scripted | 103 | 290472 | 10 | 1044525 | Mario Carles | 7ff3b498bdd10c2d | 1 | 1 | 1 | 1 | no | scripted_semantics_require_live_confirmation, open_route_not_closed, route_not_repeated_end_to_end, single_identity_generation_support, insufficient_route_geometry; path=(3436.0,3.0,760.5) -> (3413.0,3.0,755.5) |
| 35 | scripted | 137 | 30365 | 10 | 1044525 | Lolly the Reet | faeb5ed73b60bee4 | 1 | 1 | 1 | 1 | no | scripted_semantics_require_live_confirmation, open_route_not_closed, route_not_repeated_end_to_end, single_identity_generation_support, insufficient_route_geometry; path=(3360.0,3.5,620.0) -> (3358.0,3.5,640.5) |
| 25 | scripted | 103 | 26149 | 23 | 1044525 | Janae Seaman | 47be72fccbfdc686 | 45 | 1 | 1 | 13 | no | scripted_semantics_require_live_confirmation, open_route_not_closed, branched_route_requires_live_confirmation, route_not_repeated_end_to_end, single_identity_generation_support; path=(3460.0,9.0,883.0) -> (3468.5,9.0,883.5) -> (3472.0,9.0,883.5) -> (3472.5,9.0,885.5) -> (3473.5,9.0,888.0) -> (3471.0,9.0,889.0) -> (3468.5,9.0,889.0) -> (3466.5,9.0,888.0) -> … (+38) |

## Reject with exact reason

| Score | Classification | Family | Template | Level | PF | Names | Signature | Paths | IDs | Generations | Edges | Closed | Decision |
| ---: | --- | ---: | ---: | ---: | ---: | --- | --- | ---: | ---: | ---: | ---: | --- | --- |
| 49 | spawn | unresolved | unresolved | unresolved | unresolved | Malfunctioning Cleaning Robot | 3cc182eb60a865ad | 3 | 2 | 2 | 1 | yes | metadata_missing, spawn_transient_not_patrol, teleport_or_position_discontinuity |
| 49 | patrol | unresolved | unresolved | unresolved | unresolved | Malfunctioning Cleaning Robot | 42c9e72a057e452a | 4 | 2 | 2 | 1 | yes | metadata_missing, teleport_or_position_discontinuity |
| 49 | patrol | unresolved | unresolved | unresolved | unresolved | Malfunctioning Cleaning Robot | c38ae19a41cb3674 | 2 | 2 | 2 | 1 | yes | metadata_missing, path_interruption, teleport_or_position_discontinuity |
| 49 | patrol | unresolved | unresolved | unresolved | unresolved | Malfunctioning Cleaning Robot | e8a45009016813e8 | 2 | 2 | 2 | 1 | yes | metadata_missing, teleport_or_position_discontinuity |
| 49 | spawn | 0 | 26092 | 40 | 1044525 | ICC Peacekeeper | 02e9052808e591a4 | 1 | 1 | 1 | 1 | no | spawn_transient_not_patrol |
| 49 | spawn | 0 | 26092 | 40 | 1044525 | ICC Peacekeeper | 08d4ef1dac7231fa | 1 | 1 | 1 | 1 | no | spawn_transient_not_patrol |
| 49 | spawn | 0 | 26092 | 40 | 1044525 | ICC Peacekeeper | 72c723fc985bc8f0 | 1 | 1 | 1 | 1 | no | spawn_transient_not_patrol |
| 49 | spawn | 0 | 26092 | 40 | 1044525 | ICC Peacekeeper | 74a401cf538cc67a | 2 | 1 | 1 | 2 | no | spawn_transient_not_patrol |
| 49 | spawn | 0 | 26092 | 40 | 1044525 | ICC Peacekeeper | 88da6d65ef253592 | 1 | 1 | 1 | 1 | no | spawn_transient_not_patrol |
| 49 | spawn | 0 | 26092 | 40 | 1044525 | ICC Peacekeeper | b892bc7ff7df1aab | 2 | 1 | 1 | 2 | no | spawn_transient_not_patrol |
| 49 | spawn | 0 | 26092 | 40 | 1044525 | ICC Peacekeeper | c2fbfa53c4484c3e | 1 | 1 | 1 | 1 | no | spawn_transient_not_patrol |
| 49 | spawn | 15 | 17662 | 1 | 1044525 | Minibronto | 6b0224d34e7f0c7e | 1 | 1 | 1 | 1 | no | spawn_transient_not_patrol |
| 49 | spawn | 15 | 17662 | 1 | 1044525 | Minibronto | b47c92757d41470d | 2 | 1 | 1 | 2 | no | spawn_transient_not_patrol |
| 49 | spawn | 25 | 17657 | 1 | 1044525 | Garbage Flea | 0923c15dbc519044 | 1 | 1 | 1 | 1 | no | spawn_transient_not_patrol |
| 49 | spawn | 25 | 17657 | 1 | 1044525 | Garbage Flea | 107ccd1f6066e425 | 1 | 1 | 1 | 1 | no | spawn_transient_not_patrol |
| 49 | spawn | 25 | 17657 | 1 | 1044525 | Garbage Flea | 16dd19bb7c2bedcf | 2 | 1 | 2 | 1 | no | spawn_transient_not_patrol |
| 49 | spawn | 25 | 17657 | 1 | 1044525 | Garbage Flea | 2a0bc4602ec0ef70 | 1 | 1 | 1 | 1 | no | spawn_transient_not_patrol |
| 49 | spawn | 25 | 17657 | 1 | 1044525 | Garbage Flea | 48454d361a2e1472 | 1 | 1 | 1 | 1 | no | spawn_transient_not_patrol |
| 49 | spawn | 25 | 17657 | 1 | 1044525 | Garbage Flea | 6faf0312053ff97d | 3 | 1 | 3 | 1 | no | spawn_transient_not_patrol |
| 49 | spawn | 25 | 17657 | 1 | 1044525 | Garbage Flea | 7aa9f1ea48032cd1 | 1 | 1 | 1 | 1 | no | spawn_transient_not_patrol |
| 49 | spawn | 25 | 17657 | 1 | 1044525 | Garbage Flea | bccc2d0c61370037 | 2 | 1 | 1 | 2 | no | spawn_transient_not_patrol |
| 49 | spawn | 25 | 17657 | 1 | 1044525 | Garbage Flea | c633655b44bea054 | 1 | 1 | 1 | 1 | no | spawn_transient_not_patrol |
| 49 | spawn | 25 | 17657 | 1 | 1044525 | Garbage Flea | c6e78fbb33218286 | 1 | 1 | 1 | 1 | no | spawn_transient_not_patrol |
| 49 | spawn | 25 | 17657 | 1 | 1044525 | Garbage Flea | e6a07e06d41cae8c | 2 | 1 | 1 | 2 | no | spawn_transient_not_patrol |
| 49 | spawn | 25 | 17657 | 2 | 1044525 | Garbage Flea | decde73b9810c055 | 1 | 1 | 1 | 1 | no | spawn_transient_not_patrol |
| 49 | spawn | 25 | 17657 | 5 | 1044525 | Garbage Flea | 0b2b6b13ba2a0a58 | 2 | 1 | 1 | 2 | no | spawn_transient_not_patrol |
| 49 | spawn | 25 | 17657 | 5 | 1044525 | Garbage Flea | 640123f66eccb7e4 | 1 | 1 | 1 | 1 | no | spawn_transient_not_patrol |
| 49 | spawn | 25 | 17657 | 5 | 1044525 | Garbage Flea | 7dafbdf103752191 | 1 | 1 | 1 | 1 | no | spawn_transient_not_patrol |
| 49 | spawn | 25 | 17657 | 5 | 1044525 | Garbage Flea | a9d1e9b1f33d2b60 | 1 | 1 | 1 | 1 | no | spawn_transient_not_patrol |
| 49 | spawn | 25 | 17657 | 5 | 1044525 | Garbage Flea | ac2042c3d2202c68 | 1 | 1 | 1 | 1 | no | spawn_transient_not_patrol |
| 49 | spawn | 25 | 17657 | 5 | 1044525 | Garbage Flea | ae6557ae7ebca500 | 1 | 1 | 1 | 1 | no | spawn_transient_not_patrol |
| 49 | spawn | 25 | 17657 | 5 | 1044525 | Garbage Flea | c8310add78399c92 | 1 | 1 | 1 | 1 | no | spawn_transient_not_patrol |
| 49 | spawn | 25 | 17657 | 5 | 1044525 | Garbage Flea | e641384a9177f1ea | 1 | 1 | 1 | 1 | no | spawn_transient_not_patrol |
| 49 | spawn | 25 | 17657 | 6 | 1044525 | Garbage Flea | 9a97ad8061ce3fac | 2 | 1 | 1 | 2 | no | spawn_transient_not_patrol |
| 49 | spawn | 25 | 17657 | 6 | 1044525 | Garbage Flea | b44942b2a3bdc45d | 1 | 1 | 1 | 1 | no | spawn_transient_not_patrol |
| 49 | spawn | 25 | 17657 | 6 | 1044525 | Garbage Flea | bdd68826b6105f0b | 2 | 1 | 1 | 2 | no | spawn_transient_not_patrol |
| 49 | spawn | 25 | 17657 | 6 | 1044525 | Garbage Flea | e641384a9177f1ea | 1 | 1 | 1 | 1 | no | spawn_transient_not_patrol |
| 49 | spawn | 25 | 17657 | 6 | 1044525 | Garbage Flea | fb79f89b880a0467 | 1 | 1 | 1 | 1 | no | spawn_transient_not_patrol |
| 49 | spawn | 25 | 17657 | 7 | 1044525 | Mutated Garbage Flea | e4fee6755927549e | 1 | 1 | 1 | 1 | no | spawn_transient_not_patrol |
| 49 | spawn | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | f4ea642abf9d2745 | 1 | 1 | 1 | 1 | no | spawn_transient_not_patrol |
| 49 | spawn | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | e4a36c159adc5486 | 1 | 1 | 1 | 1 | no | spawn_transient_not_patrol |
| 49 | spawn | 97 | 96195 | 1 | 1044525 | Anger Manifestation | 7982b683471ea9d6 | 1 | 1 | 1 | 1 | no | spawn_transient_not_patrol |
| 49 | spawn | 97 | 96195 | 10 | 1044525 | Anger Manifestation | 96020f3d61545d45 | 1 | 1 | 1 | 1 | no | spawn_transient_not_patrol |
| 49 | spawn | 103 | 26090 | 6 | 1044525 | Janee Forejt | 1fbc681462df63fb | 1 | 1 | 1 | 1 | no | spawn_transient_not_patrol |
| 49 | spawn | 103 | 26149 | 23 | 1044525 | Janae Seaman | 3c8022baaf884458 | 1 | 1 | 1 | 1 | no | spawn_transient_not_patrol |
| 49 | spawn | 103 | 26149 | 23 | 1044525 | Janae Seaman | 669c5b4d1662a3ac | 2 | 1 | 1 | 2 | no | spawn_transient_not_patrol |
| 49 | spawn | 103 | 26149 | 23 | 1044525 | Janae Seaman | 6efffbd181ae1bfc | 3 | 1 | 1 | 3 | no | spawn_transient_not_patrol |
| 49 | spawn | 103 | 290472 | 10 | 1044525 | Mario Carles | 6965676269f903f9 | 1 | 1 | 1 | 1 | no | spawn_transient_not_patrol |
| 49 | spawn | 137 | 26125 | 10 | 1044525 | Leonora Marty | 0eb637889ef9b1bb | 1 | 1 | 1 | 1 | no | spawn_transient_not_patrol |
| 49 | spawn | 137 | 26125 | 10 | 1044525 | Leonora Marty | dbc5e828861ca159 | 3 | 1 | 3 | 1 | no | spawn_transient_not_patrol |
| 49 | spawn | 137 | 30365 | 10 | 1044525 | Lolly the Reet | 3559317d3596dba6 | 2 | 1 | 1 | 2 | no | spawn_transient_not_patrol |
| 49 | spawn | 137 | 30365 | 10 | 1044525 | Lolly the Reet | 6f63faa916515373 | 3 | 1 | 1 | 3 | no | spawn_transient_not_patrol |
| 49 | spawn | 137 | 30365 | 10 | 1044525 | Lolly the Reet | 868fd25d4339cde1 | 3 | 1 | 1 | 3 | no | spawn_transient_not_patrol |
| 49 | spawn | 137 | 30365 | 10 | 1044525 | Lolly the Reet | f5180324cec646ba | 1 | 1 | 1 | 1 | no | spawn_transient_not_patrol |
| 49 | spawn | 137 | 30365 | 10 | 1044525 | Lolly the Reet | f7fed03274cafdbe | 2 | 1 | 1 | 2 | no | spawn_transient_not_patrol |
| 49 | patrol | 1019 | 17720 | 13 | 1044525 | Robotic Guard Dog | 7e60d7b633d45f0a | 8 | 1 | 1 | 2 | yes | combat_influence, teleport_or_position_discontinuity |
| 49 | patrol | 1019 | 17720 | 13 | 1044525 | Robotic Guard Dog | 8f49a80715d1daeb | 3 | 1 | 1 | 1 | yes | combat_influence, teleport_or_position_discontinuity |
| 49 | patrol | 1019 | 17720 | 13 | 1044525 | Robotic Guard Dog | f485dfb6aa92d0fe | 2 | 1 | 1 | 1 | yes | combat_influence, teleport_or_position_discontinuity |
| 49 | patrol | 1019 | 17720 | 13 | 1044525 | Robotic Guard Dog | f4b64a3912d3f637 | 8 | 1 | 1 | 3 | yes | combat_influence, teleport_or_position_discontinuity |
| 49 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 05a9ce73ced8ff26 | 11 | 1 | 1 | 2 | yes | teleport_or_position_discontinuity |
| 49 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 05c43fabcff4984e | 3 | 1 | 1 | 1 | yes | teleport_or_position_discontinuity |
| 49 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 05fd33ef304b3ee8 | 9 | 1 | 1 | 3 | yes | teleport_or_position_discontinuity |
| 49 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 065eabb09cec8a44 | 61 | 1 | 2 | 3 | yes | teleport_or_position_discontinuity |
| 49 | spawn | 1019 | 297023 | 1 | 1044525 | Cleaning Robot | 06ee3123e3839b72 | 2 | 1 | 2 | 1 | no | spawn_transient_not_patrol |
| 49 | spawn | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 0a67058694a05f77 | 7 | 1 | 3 | 1 | yes | spawn_transient_not_patrol, teleport_or_position_discontinuity |
| 49 | spawn | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 0a7f5738dfb7abd5 | 3 | 3 | 3 | 1 | yes | incomplete_capture, spawn_transient_not_patrol, teleport_or_position_discontinuity |
| 49 | spawn | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 0f8959d246da15b5 | 1 | 1 | 1 | 1 | no | spawn_transient_not_patrol |
| 49 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 0fd4e69a9754926e | 20 | 1 | 1 | 3 | yes | teleport_or_position_discontinuity |
| 49 | spawn | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 0ffad9c0b09418a7 | 1 | 1 | 1 | 1 | no | spawn_transient_not_patrol |
| 49 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 105c18cf412c89a7 | 33 | 1 | 2 | 3 | yes | teleport_or_position_discontinuity |
| 49 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 11485d2777a15d24 | 2 | 1 | 1 | 1 | yes | teleport_or_position_discontinuity |
| 49 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 11b197df7d1f8c30 | 10 | 1 | 1 | 3 | yes | teleport_or_position_discontinuity |
| 49 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 1508d47242fa76e7 | 3 | 2 | 2 | 1 | yes | teleport_or_position_discontinuity |
| 49 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 159424fc2edfd851 | 61 | 1 | 2 | 4 | yes | teleport_or_position_discontinuity |
| 49 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 177c2c2d6b1b54fc | 7 | 1 | 1 | 2 | yes | teleport_or_position_discontinuity |
| 49 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 17d51af64389491c | 2 | 1 | 1 | 1 | yes | teleport_or_position_discontinuity |
| 49 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 1af76546eeb8d30a | 25 | 1 | 2 | 3 | yes | teleport_or_position_discontinuity |
| 49 | spawn | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 1b4258f5837132bb | 1 | 1 | 1 | 1 | no | spawn_transient_not_patrol |
| 49 | spawn | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 1ca08be04ff00d42 | 1 | 1 | 1 | 1 | no | spawn_transient_not_patrol |
| 49 | spawn | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 21c9373d5f418204 | 3 | 1 | 1 | 2 | yes | spawn_transient_not_patrol |
| 49 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 23a098ca1953fa86 | 5 | 1 | 1 | 3 | yes | teleport_or_position_discontinuity |
| 49 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 24ae3969c6aef26d | 2 | 1 | 1 | 1 | yes | teleport_or_position_discontinuity |
| 49 | spawn | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 24b256a032caa058 | 1 | 1 | 1 | 1 | no | spawn_transient_not_patrol |
| 49 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 26a5df598c7f3d92 | 2 | 2 | 2 | 1 | yes | teleport_or_position_discontinuity |
| 49 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 31bbe165dc5aaee2 | 7 | 2 | 2 | 2 | yes | teleport_or_position_discontinuity |
| 49 | spawn | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 31d7afb181ee31e3 | 1 | 1 | 1 | 1 | yes | spawn_transient_not_patrol |
| 49 | spawn | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 379a9b24d0339be2 | 14 | 6 | 7 | 1 | yes | incomplete_capture, spawn_transient_not_patrol, teleport_or_position_discontinuity |
| 49 | spawn | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 381adf34e6c50aec | 41 | 1 | 2 | 3 | yes | spawn_transient_not_patrol, teleport_or_position_discontinuity |
| 49 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 39b77f866bb2a1c5 | 6 | 3 | 3 | 1 | yes | incomplete_capture, teleport_or_position_discontinuity |
| 49 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 3d9cfa239c48b550 | 6 | 1 | 1 | 3 | yes | teleport_or_position_discontinuity |
| 49 | spawn | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 40a7348d85e87101 | 67 | 1 | 2 | 3 | yes | spawn_transient_not_patrol, teleport_or_position_discontinuity |
| 49 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 42c9e72a057e452a | 4 | 1 | 1 | 1 | yes | teleport_or_position_discontinuity |
| 49 | spawn | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 43c1f80101645c08 | 92 | 1 | 2 | 3 | yes | spawn_transient_not_patrol, teleport_or_position_discontinuity |
| 49 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 4b8b8bfef4cdc682 | 64 | 1 | 2 | 3 | yes | teleport_or_position_discontinuity |
| 49 | spawn | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 511a9c71ee37dfa4 | 11 | 3 | 4 | 1 | yes | incomplete_capture, spawn_transient_not_patrol, teleport_or_position_discontinuity |
| 49 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 5413cab18e985c3a | 2 | 1 | 1 | 1 | yes | teleport_or_position_discontinuity |
| 49 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 5b92a085dbfa0abf | 6 | 1 | 1 | 3 | yes | teleport_or_position_discontinuity |
| 49 | spawn | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 5bce84d156389ec2 | 1 | 1 | 1 | 1 | no | spawn_transient_not_patrol |
| 49 | spawn | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 5c6300e3a9bfe6d2 | 1 | 1 | 1 | 1 | no | spawn_transient_not_patrol |
| 49 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 5ce128c05dab9cc8 | 9 | 2 | 2 | 1 | yes | teleport_or_position_discontinuity |
| 49 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 5ef1bfa6b88685f0 | 2 | 2 | 2 | 1 | yes | teleport_or_position_discontinuity |
| 49 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 604c0e789def533d | 2 | 2 | 2 | 1 | yes | teleport_or_position_discontinuity |
| 49 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 651d7b89f080711b | 7 | 1 | 2 | 2 | yes | teleport_or_position_discontinuity |
| 49 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 6a642bf94a908db6 | 5 | 1 | 1 | 3 | yes | teleport_or_position_discontinuity |
| 49 | spawn | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 6d9c1341de85d1c7 | 7 | 3 | 3 | 1 | yes | spawn_transient_not_patrol, teleport_or_position_discontinuity |
| 49 | spawn | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 6df98caba986712f | 230 | 1 | 2 | 4 | yes | spawn_transient_not_patrol, teleport_or_position_discontinuity |
| 49 | spawn | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 6e437c8a1f5b457c | 3 | 1 | 1 | 3 | no | spawn_transient_not_patrol |
| 49 | spawn | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 7442c1dae42baf78 | 4 | 3 | 3 | 1 | yes | spawn_transient_not_patrol, teleport_or_position_discontinuity |
| 49 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 761a49258fff20d1 | 2 | 1 | 1 | 1 | yes | teleport_or_position_discontinuity |
| 49 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 7911d11e5ac44f10 | 3 | 1 | 1 | 3 | yes | teleport_or_position_discontinuity |
| 49 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 7f53076e3e89ddb0 | 3 | 1 | 1 | 3 | yes | teleport_or_position_discontinuity |
| 49 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 7fedc97aa0d790b0 | 2 | 1 | 1 | 1 | yes | teleport_or_position_discontinuity |
| 49 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 80bcf69463bc0cb9 | 5 | 3 | 3 | 1 | yes | teleport_or_position_discontinuity |
| 49 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 811726f2b0bd01f1 | 25 | 1 | 1 | 2 | yes | teleport_or_position_discontinuity |
| 49 | spawn | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 8419cfb2cad60906 | 237 | 1 | 3 | 2 | yes | spawn_transient_not_patrol, teleport_or_position_discontinuity |
| 49 | spawn | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 89e03631e657442c | 1 | 1 | 1 | 1 | no | spawn_transient_not_patrol |
| 49 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 8aa47aab737d7ee6 | 75 | 1 | 2 | 4 | yes | teleport_or_position_discontinuity |
| 49 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 8d9f4c777ebc4c42 | 4 | 4 | 4 | 1 | yes | teleport_or_position_discontinuity |
| 49 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 8e10fac6ebb4355b | 2 | 1 | 1 | 1 | yes | teleport_or_position_discontinuity |
| 49 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 91f2832296c823d2 | 4 | 2 | 2 | 1 | yes | teleport_or_position_discontinuity |
| 49 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 941836b0874ffad3 | 3 | 2 | 2 | 1 | yes | teleport_or_position_discontinuity |
| 49 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 94a97f4f69c371c7 | 3 | 3 | 3 | 1 | no | path_interruption |
| 49 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 95d8c80958055365 | 2 | 1 | 1 | 1 | yes | teleport_or_position_discontinuity |
| 49 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 965c331b846b5ecf | 15 | 1 | 4 | 2 | yes | teleport_or_position_discontinuity |
| 49 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 96b731b204fd5ee5 | 6 | 5 | 5 | 1 | yes | incomplete_capture, teleport_or_position_discontinuity |
| 49 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 9f0b69e78c41f71b | 2 | 2 | 2 | 1 | yes | teleport_or_position_discontinuity |
| 49 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 9f3fc86a4ba3cc03 | 30 | 1 | 2 | 3 | yes | teleport_or_position_discontinuity |
| 49 | spawn | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 9fbfc0bf1593b13f | 228 | 1 | 4 | 2 | yes | spawn_transient_not_patrol, teleport_or_position_discontinuity |
| 49 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | a00cf2313c26d222 | 2 | 1 | 1 | 1 | yes | teleport_or_position_discontinuity |
| 49 | spawn | 1019 | 297023 | 1 | 1044525 | Cleaning Robot | a2f777e445483774 | 1 | 1 | 1 | 1 | no | spawn_transient_not_patrol |
| 49 | spawn | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | a330656d41d4477d | 3 | 1 | 1 | 2 | yes | spawn_transient_not_patrol |
| 49 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | a39145e2d1bbd4a2 | 13 | 1 | 1 | 2 | yes | teleport_or_position_discontinuity |
| 49 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | a77cae23e768199c | 3 | 2 | 2 | 1 | yes | incomplete_capture, path_interruption, teleport_or_position_discontinuity |
| 49 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | a8780d9bf86d2c55 | 5 | 1 | 1 | 3 | yes | teleport_or_position_discontinuity |
| 49 | spawn | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | ae9985f9a60aae58 | 24 | 1 | 2 | 1 | yes | spawn_transient_not_patrol, teleport_or_position_discontinuity |
| 49 | spawn | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | b23f46e86a98a5dc | 1 | 1 | 1 | 1 | no | spawn_transient_not_patrol |
| 49 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | b61eee0b45076dbc | 3 | 1 | 1 | 3 | yes | teleport_or_position_discontinuity |
| 49 | spawn | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | bd7ef54282e7e33f | 15 | 3 | 4 | 2 | yes | spawn_transient_not_patrol, teleport_or_position_discontinuity |
| 49 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | be37bc0f3c7842cc | 3 | 1 | 1 | 1 | yes | teleport_or_position_discontinuity |
| 49 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | be7b35ca44b72e8e | 9 | 1 | 1 | 3 | yes | teleport_or_position_discontinuity |
| 49 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | bf375643cc170bbd | 7 | 1 | 1 | 2 | yes | teleport_or_position_discontinuity |
| 49 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | c38ae19a41cb3674 | 2 | 1 | 1 | 1 | yes | path_interruption, teleport_or_position_discontinuity |
| 49 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | c6d600f0b03f8e9f | 2 | 1 | 1 | 1 | yes | teleport_or_position_discontinuity |
| 49 | spawn | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | c9791f310909c194 | 12 | 2 | 2 | 2 | yes | spawn_transient_not_patrol, teleport_or_position_discontinuity |
| 49 | spawn | 1019 | 297023 | 1 | 1044525 | Cleaning Robot | cad99f26864855ad | 1 | 1 | 1 | 1 | no | spawn_transient_not_patrol |
| 49 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | cb6fa4e44d5960f8 | 3 | 1 | 1 | 3 | yes | teleport_or_position_discontinuity |
| 49 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | cc601b1388ea4c62 | 2 | 1 | 1 | 1 | yes | teleport_or_position_discontinuity |
| 49 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | cdfa0eaf61526767 | 18 | 1 | 2 | 2 | yes | teleport_or_position_discontinuity |
| 49 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | d1d91c8ea5fab72b | 3 | 1 | 1 | 3 | yes | teleport_or_position_discontinuity |
| 49 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | ddf07bdd4dc16422 | 3 | 2 | 2 | 1 | yes | incomplete_capture, teleport_or_position_discontinuity |
| 49 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | df6c5dc2a40fd397 | 4 | 1 | 1 | 3 | yes | teleport_or_position_discontinuity |
| 49 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | e18cbb9d0c67a148 | 16 | 1 | 1 | 3 | yes | teleport_or_position_discontinuity |
| 49 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | e234527da6011f7f | 2 | 1 | 1 | 1 | yes | teleport_or_position_discontinuity |
| 49 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | eeb53d7ce488c1a5 | 2 | 2 | 2 | 1 | yes | teleport_or_position_discontinuity |
| 49 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | f3cbeb1444565a63 | 3 | 2 | 2 | 1 | yes | teleport_or_position_discontinuity |
| 49 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | f476e43ea6891225 | 4 | 1 | 1 | 3 | yes | path_interruption |
| 49 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | f52fa3d80aab4ca3 | 9 | 1 | 1 | 4 | yes | teleport_or_position_discontinuity |
| 49 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | f7a350df28cd5ba0 | 6 | 1 | 1 | 3 | yes | teleport_or_position_discontinuity |
| 49 | spawn | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | f8b47a44c79eff37 | 3 | 1 | 1 | 3 | yes | spawn_transient_not_patrol, teleport_or_position_discontinuity |
| 49 | spawn | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | f8c1c9092fb2466b | 519 | 1 | 5 | 2 | yes | spawn_transient_not_patrol, teleport_or_position_discontinuity |
| 49 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | fc5cc2670c2c405d | 3 | 1 | 1 | 3 | yes | path_interruption |
| 49 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | fc993505791de718 | 4 | 2 | 2 | 2 | yes | incomplete_capture, teleport_or_position_discontinuity |
| 49 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | fddf5570bff88f69 | 71 | 1 | 2 | 4 | yes | teleport_or_position_discontinuity |
| 45 | patrol | 1019 | 17720 | 13 | 1044525 | Robotic Guard Dog | 42e1cc6537e9c26f | 2 | 1 | 1 | 1 | yes | combat_influence, path_interruption |
| 45 | patrol | 1019 | 17720 | 13 | 1044525 | Robotic Guard Dog | 50ab0637e33b02b0 | 1 | 1 | 1 | 1 | yes | combat_influence, teleport_or_position_discontinuity |
| 45 | patrol | 1019 | 17720 | 13 | 1044525 | Robotic Guard Dog | b4d7d6c896d7516a | 3 | 1 | 1 | 2 | yes | combat_influence, teleport_or_position_discontinuity |
| 45 | patrol | 1019 | 17720 | 13 | 1044525 | Robotic Guard Dog | eea0d39fead1fd73 | 3 | 1 | 1 | 2 | yes | combat_influence |
| 45 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 10a5973a6f672870 | 4 | 1 | 1 | 2 | yes | teleport_or_position_discontinuity |
| 45 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 2339281269027587 | 1 | 1 | 1 | 1 | yes | teleport_or_position_discontinuity |
| 45 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 291d91fec3ea8e1d | 3 | 1 | 1 | 2 | yes | teleport_or_position_discontinuity |
| 45 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 2cbc575941bfd1aa | 15 | 1 | 1 | 4 | yes | teleport_or_position_discontinuity |
| 45 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 2f16c61b8691ea39 | 3 | 1 | 1 | 2 | yes | teleport_or_position_discontinuity |
| 45 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 33372ea540c553ab | 1 | 1 | 1 | 1 | yes | teleport_or_position_discontinuity |
| 45 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 37f6d79cee378ec9 | 3 | 1 | 1 | 1 | yes | teleport_or_position_discontinuity |
| 45 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 3fa87b8d443f0602 | 1 | 1 | 1 | 1 | yes | path_interruption, teleport_or_position_discontinuity |
| 45 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 40c801dac547b6ec | 1 | 1 | 1 | 1 | yes | teleport_or_position_discontinuity |
| 45 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 42aeaf0888809f9c | 1 | 1 | 1 | 1 | yes | teleport_or_position_discontinuity |
| 45 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 4e825f3fb2d75848 | 1 | 1 | 1 | 1 | yes | teleport_or_position_discontinuity |
| 45 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 607e77558e4536e3 | 2 | 1 | 1 | 2 | yes | teleport_or_position_discontinuity |
| 45 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 64e03a05d527090e | 1 | 1 | 1 | 1 | yes | teleport_or_position_discontinuity |
| 45 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 7ad6a7c770661002 | 84 | 1 | 1 | 4 | yes | teleport_or_position_discontinuity |
| 45 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 7b68cf9d9fe14980 | 3 | 1 | 1 | 2 | yes | teleport_or_position_discontinuity |
| 45 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 8b1cc3ee512808d1 | 4 | 1 | 1 | 2 | yes | teleport_or_position_discontinuity |
| 45 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 8c991eba3e3c5f28 | 3 | 1 | 1 | 2 | yes | teleport_or_position_discontinuity |
| 45 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 9521673998436b6f | 1 | 1 | 1 | 1 | yes | teleport_or_position_discontinuity |
| 45 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 9b6ecdddec4aaefa | 3 | 1 | 1 | 2 | yes | teleport_or_position_discontinuity |
| 45 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 9ee854e9625c556f | 1 | 1 | 1 | 1 | yes | teleport_or_position_discontinuity |
| 45 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 9f4cff87203f91c4 | 1 | 1 | 1 | 1 | yes | teleport_or_position_discontinuity |
| 45 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | a159862586755f97 | 1 | 1 | 1 | 1 | yes | teleport_or_position_discontinuity |
| 45 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | a1eb93730999833e | 6 | 1 | 1 | 2 | yes | teleport_or_position_discontinuity |
| 45 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | a63cf678acf822af | 2 | 1 | 1 | 2 | yes | teleport_or_position_discontinuity |
| 45 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | b17b07d0be6be2d1 | 2 | 1 | 1 | 2 | yes | teleport_or_position_discontinuity |
| 45 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | b438d94f831a6343 | 3 | 1 | 1 | 2 | yes | teleport_or_position_discontinuity |
| 45 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | c5cb7d09d64163c5 | 1 | 1 | 1 | 1 | yes | teleport_or_position_discontinuity |
| 45 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | c7a65fdc7778b3f9 | 1 | 1 | 1 | 1 | yes | teleport_or_position_discontinuity |
| 45 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | c7eafc2112e4c423 | 1 | 1 | 1 | 1 | yes | teleport_or_position_discontinuity |
| 45 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | cae8235008321696 | 1 | 1 | 1 | 1 | yes | teleport_or_position_discontinuity |
| 45 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | d1835d62f6cbd272 | 1 | 1 | 1 | 1 | yes | teleport_or_position_discontinuity |
| 45 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | daf58461ae1da960 | 1 | 1 | 1 | 1 | yes | teleport_or_position_discontinuity |
| 45 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | dd836379e1e7c748 | 4 | 1 | 1 | 2 | yes | path_interruption |
| 45 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | e1c23e541ff7e3c9 | 1 | 1 | 1 | 1 | yes | path_interruption, teleport_or_position_discontinuity |
| 45 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | ed3c3528c8ba7fce | 4 | 1 | 1 | 2 | yes | teleport_or_position_discontinuity |
| 45 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | ef323426e1447448 | 1 | 1 | 1 | 1 | yes | teleport_or_position_discontinuity |
| 45 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | f573843339d4d162 | 3 | 1 | 1 | 2 | yes | teleport_or_position_discontinuity |
| 45 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | f684316e9dc8eec4 | 1 | 1 | 1 | 1 | yes | teleport_or_position_discontinuity |
| 45 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | fecb4de7b86e821a | 1 | 1 | 1 | 1 | yes | teleport_or_position_discontinuity |
| 40 | patrol | unresolved | unresolved | unresolved | unresolved | Malfunctioning Cleaning Robot | 105c18cf412c89a7 | 15 | 1 | 1 | 3 | yes | metadata_missing, teleport_or_position_discontinuity |
| 40 | patrol | unresolved | unresolved | unresolved | unresolved | Malfunctioning Cleaning Robot | 3d9cfa239c48b550 | 14 | 1 | 1 | 3 | yes | metadata_missing, teleport_or_position_discontinuity |
| 40 | patrol | unresolved | unresolved | unresolved | unresolved | Malfunctioning Cleaning Robot | 43c1f80101645c08 | 11 | 1 | 1 | 3 | yes | metadata_missing, teleport_or_position_discontinuity |
| 40 | spawn | 0 | 26092 | 40 | 1044525 | ICC Peacekeeper | 49c48a7309a4fdea | 4 | 1 | 4 | 1 | yes | incomplete_capture, path_interruption, spawn_transient_not_patrol, teleport_or_position_discontinuity |
| 40 | patrol | 25 | 17657 | 2 | 1044525 | Garbage Flea | c543b21798c97ab1 | 138 | 2 | 3 | 4 | no | incomplete_capture |
| 40 | spawn | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 0aa0e57589d24379 | 335 | 1 | 5 | 2 | yes | incomplete_capture, spawn_transient_not_patrol, teleport_or_position_discontinuity |
| 40 | spawn | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 5f5bfc1a378c7012 | 246 | 1 | 5 | 1 | yes | incomplete_capture, spawn_transient_not_patrol, teleport_or_position_discontinuity |
| 40 | spawn | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 5fca3e32a0d19813 | 108 | 1 | 5 | 1 | yes | incomplete_capture, spawn_transient_not_patrol, teleport_or_position_discontinuity |
| 40 | spawn | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | a36ab3c3e122b222 | 122 | 1 | 4 | 1 | yes | incomplete_capture, spawn_transient_not_patrol, teleport_or_position_discontinuity |
| 40 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | b491a2c2c0b85fe8 | 11 | 1 | 2 | 2 | yes | incomplete_capture, teleport_or_position_discontinuity |
| 40 | spawn | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | b4d12b4bb9e43c87 | 160 | 1 | 4 | 1 | yes | incomplete_capture, spawn_transient_not_patrol, teleport_or_position_discontinuity |
| 35 | patrol | unresolved | unresolved | unresolved | unresolved | Malfunctioning Cleaning Robot | 0a67058694a05f77 | 2 | 1 | 1 | 1 | yes | metadata_missing, teleport_or_position_discontinuity |
| 35 | spawn | unresolved | unresolved | unresolved | unresolved | Malfunctioning Cleaning Robot | 0aa0e57589d24379 | 71 | 1 | 1 | 2 | yes | metadata_missing, spawn_transient_not_patrol, teleport_or_position_discontinuity |
| 35 | patrol | unresolved | unresolved | unresolved | unresolved | Malfunctioning Cleaning Robot | 3439d3b1ea7698bd | 2 | 1 | 1 | 1 | yes | metadata_missing, teleport_or_position_discontinuity |
| 35 | patrol | unresolved | unresolved | unresolved | unresolved | Malfunctioning Cleaning Robot | 5fca3e32a0d19813 | 28 | 1 | 1 | 1 | yes | metadata_missing, teleport_or_position_discontinuity |
| 35 | spawn | unresolved | unresolved | unresolved | unresolved | Malfunctioning Cleaning Robot | 9fbfc0bf1593b13f | 70 | 1 | 1 | 2 | yes | metadata_missing, spawn_transient_not_patrol, teleport_or_position_discontinuity |
| 35 | spawn | unresolved | unresolved | unresolved | unresolved | Malfunctioning Cleaning Robot | a36ab3c3e122b222 | 43 | 1 | 1 | 1 | yes | metadata_missing, spawn_transient_not_patrol, teleport_or_position_discontinuity |
| 35 | patrol | unresolved | unresolved | unresolved | unresolved | Malfunctioning Cleaning Robot | c6d600f0b03f8e9f | 2 | 1 | 1 | 1 | yes | metadata_missing, teleport_or_position_discontinuity |
| 35 | patrol | unresolved | unresolved | unresolved | unresolved | Malfunctioning Cleaning Robot | cae8235008321696 | 3 | 1 | 1 | 1 | yes | metadata_missing, teleport_or_position_discontinuity |
| 35 | patrol | unresolved | unresolved | unresolved | unresolved | Malfunctioning Cleaning Robot | ea8fab34faaa7b15 | 2 | 1 | 1 | 1 | yes | metadata_missing, teleport_or_position_discontinuity |
| 35 | patrol | 0 | 26092 | 40 | 1044525 | ICC Peacekeeper | 578831048ace3b40 | 4 | 1 | 1 | 4 | no | combat_influence |
| 35 | patrol | 15 | 17662 | 1 | 1044525 | Minibronto | bb7c374931545794 | 4 | 1 | 1 | 4 | no | combat_influence |
| 35 | combat chase | 15 | 17662 | 1 | 1044525 | Minibronto | e6183b668b5b1ed8 | 4 | 1 | 1 | 4 | no | combat_chase, combat_influence |
| 35 | spawn | 25 | 17657 | 2 | 1044525 | Garbage Flea | c479c5207020f85b | 3 | 2 | 3 | 1 | no | incomplete_capture, spawn_transient_not_patrol |
| 35 | spawn | 25 | 17657 | 5 | 1044525 | Garbage Flea | 071c65c767515720 | 2 | 2 | 2 | 1 | no | incomplete_capture, path_interruption, spawn_transient_not_patrol |
| 35 | patrol | 25 | 17657 | 5 | 1044525 | Garbage Flea | 12860401c2ca48e9 | 5 | 1 | 1 | 5 | no | path_interruption |
| 35 | spawn | 25 | 17657 | 5 | 1044525 | Garbage Flea | 3205151eb0b1946c | 2 | 2 | 2 | 1 | no | incomplete_capture, spawn_transient_not_patrol |
| 35 | spawn | 25 | 17657 | 5 | 1044525 | Garbage Flea | 79aba93092eb384b | 2 | 2 | 2 | 1 | no | incomplete_capture, path_interruption, spawn_transient_not_patrol |
| 35 | combat chase | 25 | 17657 | 5 | 1044525 | Garbage Flea | d6fc4845cd3bb133 | 4 | 1 | 1 | 4 | no | combat_chase, combat_influence, player_influence |
| 35 | combat chase | 25 | 17657 | 5 | 1044525 | Garbage Flea | e92c1f5fcc7e90b7 | 5 | 1 | 1 | 5 | no | combat_chase, combat_influence, player_influence |
| 35 | spawn | 25 | 17657 | 5 | 1044525 | Garbage Flea | eb0df0e859c80a00 | 3 | 1 | 1 | 3 | no | path_interruption, spawn_transient_not_patrol |
| 35 | spawn | 25 | 17657 | 5 | 1044525 | Garbage Flea | f1a890ecc577b29d | 2 | 2 | 2 | 1 | no | incomplete_capture, spawn_transient_not_patrol, teleport_or_position_discontinuity |
| 35 | combat chase | 25 | 17657 | 6 | 1044525 | Garbage Flea | 2856db55adeb63ab | 5 | 2 | 5 | 1 | no | combat_chase, combat_influence, incomplete_capture, path_interruption, player_influence, teleport_or_position_discontinuity |
| 35 | spawn | 25 | 17657 | 6 | 1044525 | Garbage Flea | 2d377ecc8d8e3881 | 7 | 2 | 7 | 1 | no | incomplete_capture, path_interruption, spawn_transient_not_patrol |
| 35 | spawn | 25 | 17657 | 6 | 1044525 | Garbage Flea | 631233674e54552f | 4 | 3 | 4 | 1 | no | incomplete_capture, path_interruption, spawn_transient_not_patrol |
| 35 | spawn | 25 | 17657 | 6 | 1044525 | Garbage Flea | 79aba93092eb384b | 10 | 3 | 10 | 1 | no | incomplete_capture, path_interruption, spawn_transient_not_patrol |
| 35 | patrol | 25 | 17657 | 6 | 1044525 | Garbage Flea | 95881d2856f87aa8 | 3 | 2 | 3 | 1 | no | incomplete_capture, teleport_or_position_discontinuity |
| 35 | combat chase | 25 | 17657 | 7 | 1044525 | Mutated Garbage Flea | bf38113c48f730f4 | 5 | 1 | 1 | 5 | no | combat_chase, combat_influence, player_influence |
| 35 | combat chase | 53 | 30365 | 6 | 1044525 | Desert Reet | 613a0ec8ca9d6a7e | 3 | 1 | 1 | 3 | no | combat_chase, combat_influence, player_influence |
| 35 | combat chase | 55 | 17687 | 5 | 1044525 | Rollerrat | 6117f833b26aa76b | 3 | 1 | 1 | 3 | no | combat_chase, combat_influence, player_influence |
| 35 | combat chase | 55 | 17687 | 6 | 1044525 | Rollerrat | edf61db552404a90 | 3 | 1 | 1 | 3 | no | combat_chase, combat_influence, player_influence |
| 35 | patrol | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | 0f01991ec2b338bc | 10 | 1 | 1 | 10 | no | combat_influence |
| 35 | patrol | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | 14627aa83c4aeb19 | 3 | 1 | 1 | 3 | no | combat_influence |
| 35 | patrol | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | 165ca2772e9a354d | 12 | 1 | 1 | 12 | no | path_interruption |
| 35 | patrol | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | 26ea154bc23b4815 | 5 | 1 | 1 | 5 | no | path_interruption |
| 35 | patrol | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | 28c588683c84707f | 10 | 1 | 1 | 10 | no | path_interruption |
| 35 | patrol | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | 3cd83be4abb558cc | 3 | 1 | 1 | 3 | no | combat_influence |
| 35 | patrol | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | 3cdd446c1122a64a | 8 | 1 | 1 | 8 | no | combat_influence |
| 35 | patrol | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | 434b46a44af0f89b | 3 | 1 | 1 | 3 | no | combat_influence |
| 35 | patrol | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | 434b6b280681bd53 | 5 | 1 | 1 | 5 | no | combat_influence |
| 35 | combat chase | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | 49333195cae7e418 | 3 | 1 | 1 | 3 | no | combat_chase, combat_influence, path_interruption |
| 35 | leash | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | 51ce9012220a3091 | 3 | 1 | 1 | 3 | no | combat_influence, leash_after_combat, path_interruption |
| 35 | combat chase | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | 52bee5fbbbc015ac | 3 | 1 | 1 | 3 | no | combat_chase, combat_influence, path_interruption |
| 35 | leash | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | 564d9beacb8bdff0 | 5 | 1 | 1 | 5 | no | combat_influence, leash_after_combat |
| 35 | leash | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | 5d56717f1529ab0d | 4 | 1 | 1 | 4 | no | combat_influence, leash_after_combat |
| 35 | patrol | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | 67005fcd0e265b93 | 19 | 1 | 1 | 19 | no | path_interruption |
| 35 | patrol | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | 6f260207be80cfec | 19 | 1 | 1 | 19 | no | path_interruption |
| 35 | leash | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | 71bbafb52808e2d3 | 6 | 1 | 1 | 6 | no | combat_influence, leash_after_combat |
| 35 | combat chase | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | 730e45f133915584 | 4 | 1 | 1 | 4 | no | combat_chase, combat_influence, path_interruption |
| 35 | combat chase | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | 7e0c175ed3d709ef | 3 | 1 | 1 | 3 | no | combat_chase, combat_influence, teleport_or_position_discontinuity |
| 35 | patrol | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | 933ea1db50e4c19f | 8 | 1 | 1 | 8 | no | path_interruption |
| 35 | patrol | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | 9b64fb2ec2114832 | 8 | 1 | 1 | 8 | no | combat_influence |
| 35 | leash | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | 9bb5bd2b86b47eec | 6 | 1 | 1 | 6 | no | combat_influence, leash_after_combat |
| 35 | patrol | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | 9bce3aa82cccbffd | 4 | 1 | 1 | 4 | no | path_interruption |
| 35 | patrol | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | a1e867f87aa9ade8 | 3 | 1 | 1 | 3 | no | combat_influence |
| 35 | patrol | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | a27eca56d98f2f6c | 5 | 1 | 1 | 5 | no | combat_influence |
| 35 | leash | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | a3a257509911a937 | 7 | 1 | 1 | 7 | no | combat_influence, leash_after_combat, path_interruption |
| 35 | leash | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | a6b9c706b4833448 | 3 | 1 | 1 | 3 | no | combat_influence, leash_after_combat |
| 35 | patrol | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | c727abe8847dfbd3 | 5 | 1 | 1 | 5 | no | path_interruption |
| 35 | combat chase | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | c841c5b68daead4c | 3 | 1 | 1 | 3 | no | combat_chase, combat_influence, path_interruption |
| 35 | patrol | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | d6b1bb79ee98e079 | 5 | 1 | 1 | 5 | no | combat_influence |
| 35 | patrol | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | d6d57d81652171e0 | 3 | 1 | 1 | 3 | no | combat_influence |
| 35 | patrol | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | dad70d2bbdddde9d | 4 | 1 | 1 | 4 | no | combat_influence |
| 35 | combat chase | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | e99080a8291f64f6 | 3 | 1 | 1 | 3 | no | combat_chase, combat_influence, teleport_or_position_discontinuity |
| 35 | leash | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | f206dd1427df9ea1 | 5 | 1 | 1 | 5 | no | combat_influence, leash_after_combat, path_interruption |
| 35 | combat chase | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | f8ae3df6aebafb61 | 3 | 1 | 1 | 3 | no | combat_chase, combat_influence, path_interruption |
| 35 | patrol | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | f95b28231acb378b | 9 | 1 | 1 | 9 | no | teleport_or_position_discontinuity |
| 35 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 1c6dc240bfbdc882 | 6 | 1 | 1 | 6 | no | path_interruption |
| 35 | leash | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 2683794c37613b32 | 6 | 1 | 1 | 6 | no | combat_influence, leash_after_combat |
| 35 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 27ee8313a33a6548 | 6 | 1 | 1 | 6 | no | combat_influence |
| 35 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 2c054dda11702b71 | 4 | 1 | 1 | 4 | no | combat_influence |
| 35 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 316e2ce1cfb78d6e | 3 | 1 | 1 | 3 | no | combat_influence |
| 35 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 375d67300a3c4781 | 9 | 1 | 1 | 9 | no | combat_influence |
| 35 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 4606a006c65f5f4b | 14 | 1 | 1 | 14 | no | path_interruption |
| 35 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 4c3e72535627a238 | 8 | 1 | 1 | 8 | no | combat_influence |
| 35 | leash | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 572ac0fe5e69dea5 | 6 | 1 | 1 | 6 | no | combat_influence, leash_after_combat, path_interruption |
| 35 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 5bdf6c33d7b17fe7 | 8 | 1 | 1 | 8 | no | combat_influence |
| 35 | combat chase | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 5c8483dca645339b | 3 | 1 | 1 | 3 | no | combat_chase, combat_influence, path_interruption |
| 35 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 6017df576bfad8f0 | 3 | 1 | 1 | 3 | no | combat_influence |
| 35 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 7552114fa464d974 | 12 | 1 | 1 | 12 | no | path_interruption |
| 35 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 803a0fa503499c50 | 6 | 1 | 1 | 6 | no | combat_influence |
| 35 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 88654bc066402aa4 | 4 | 1 | 1 | 4 | no | combat_influence |
| 35 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 8e7fb420880b8047 | 5 | 1 | 1 | 5 | no | combat_influence |
| 35 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 92ecb3c1ec891f74 | 6 | 1 | 1 | 6 | no | combat_influence |
| 35 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 9fa0031cba7bbe51 | 3 | 1 | 1 | 3 | no | path_interruption |
| 35 | combat chase | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | a50f753c153c6696 | 3 | 1 | 1 | 3 | no | combat_chase, combat_influence, path_interruption |
| 35 | leash | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | a75253bc8370146f | 3 | 1 | 1 | 3 | no | combat_influence, leash_after_combat, path_interruption |
| 35 | leash | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | aa1dfa6951980cda | 6 | 1 | 1 | 6 | no | combat_influence, leash_after_combat |
| 35 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | b8bf25aedece9267 | 4 | 1 | 1 | 4 | no | combat_influence |
| 35 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | ba9afeb6cc361ccb | 24 | 1 | 1 | 24 | no | teleport_or_position_discontinuity |
| 35 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | bb9a8baee848b375 | 12 | 1 | 1 | 12 | no | path_interruption |
| 35 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | bc059bfaaac2d8d9 | 21 | 1 | 1 | 21 | no | path_interruption |
| 35 | leash | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | bcbec4e354621bf3 | 4 | 1 | 1 | 4 | no | combat_influence, leash_after_combat |
| 35 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | bf8e10fe81043d27 | 6 | 1 | 1 | 6 | no | combat_influence |
| 35 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | c30795a6840c7e1a | 11 | 1 | 1 | 11 | no | path_interruption |
| 35 | combat chase | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | d061678b81642aba | 3 | 1 | 1 | 3 | no | combat_chase, combat_influence |
| 35 | leash | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | e240606a21655548 | 6 | 1 | 1 | 6 | no | combat_influence, leash_after_combat |
| 35 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | ec704ba542ab1ae0 | 12 | 1 | 1 | 12 | no | path_interruption |
| 35 | leash | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | ec788340ebbd85a4 | 5 | 1 | 1 | 5 | no | combat_influence, leash_after_combat |
| 35 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | f779ac78cd1aeb95 | 3 | 1 | 1 | 3 | no | combat_influence, path_interruption |
| 35 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | 01faab8da06fb066 | 8 | 1 | 1 | 8 | no | combat_influence |
| 35 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | 02ccde1047232b5c | 3 | 1 | 1 | 3 | no | combat_influence |
| 35 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | 07a02c2f9c7de8fb | 12 | 1 | 1 | 12 | no | teleport_or_position_discontinuity |
| 35 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | 0aefed58c05d8d57 | 9 | 1 | 1 | 9 | no | teleport_or_position_discontinuity |
| 35 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | 15a06443f7557896 | 7 | 1 | 1 | 7 | no | combat_influence |
| 35 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | 17b080a7e4aa4bef | 6 | 1 | 1 | 6 | no | combat_influence |
| 35 | combat chase | 97 | 96195 | 1 | 1044525 | Anger Manifestation | 1d8a08eeb287d92d | 3 | 1 | 1 | 3 | no | combat_chase, combat_influence, path_interruption, teleport_or_position_discontinuity |
| 35 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | 247c218ae2e5bf27 | 5 | 1 | 1 | 5 | no | combat_influence |
| 35 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | 2afbe2f18250b74c | 8 | 1 | 1 | 8 | no | combat_influence |
| 35 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | 358c0f85fe1f7be9 | 6 | 1 | 1 | 6 | no | path_interruption |
| 35 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | 396cde6cf528c501 | 5 | 1 | 1 | 5 | no | combat_influence |
| 35 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | 42e28f7b585ca6cf | 5 | 1 | 1 | 5 | no | teleport_or_position_discontinuity |
| 35 | combat chase | 97 | 96195 | 1 | 1044525 | Anger Manifestation | 6121102b98d117f9 | 3 | 1 | 1 | 3 | no | combat_chase, combat_influence, path_interruption |
| 35 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | 649d3faedafb08a3 | 15 | 1 | 1 | 15 | no | path_interruption |
| 35 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | 7137980aae009f98 | 10 | 1 | 1 | 10 | no | path_interruption |
| 35 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | 7ceccc90f3a5c2c1 | 4 | 1 | 1 | 4 | no | combat_influence |
| 35 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | 7e6b3be5be2835a9 | 19 | 1 | 1 | 19 | no | path_interruption |
| 35 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | 865ba95813922259 | 7 | 1 | 1 | 7 | no | combat_influence |
| 35 | leash | 97 | 96195 | 1 | 1044525 | Anger Manifestation | 9c4bd9c7d82e03c0 | 3 | 1 | 1 | 3 | no | combat_influence, leash_after_combat |
| 35 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | 9f9e7ba38296cb96 | 44 | 1 | 1 | 44 | no | path_interruption |
| 35 | combat chase | 97 | 96195 | 1 | 1044525 | Anger Manifestation | a4ddee3dde4944b4 | 3 | 1 | 1 | 3 | no | combat_chase, combat_influence, teleport_or_position_discontinuity |
| 35 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | a91b87afbc8199e7 | 12 | 1 | 1 | 12 | no | path_interruption |
| 35 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | ae46063ac36ab2b4 | 10 | 1 | 1 | 10 | no | combat_influence |
| 35 | leash | 97 | 96195 | 1 | 1044525 | Anger Manifestation | b19acc8cb2f91c66 | 7 | 1 | 1 | 7 | no | combat_influence, leash_after_combat |
| 35 | leash | 97 | 96195 | 1 | 1044525 | Anger Manifestation | b4dffc658e0b73f9 | 4 | 1 | 1 | 4 | no | combat_influence, leash_after_combat |
| 35 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | bf0d0945cad00857 | 14 | 1 | 1 | 14 | no | path_interruption |
| 35 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | c0679a5eb6455a5e | 5 | 1 | 1 | 5 | no | combat_influence |
| 35 | combat chase | 97 | 96195 | 1 | 1044525 | Anger Manifestation | ca7a07e918091a58 | 3 | 1 | 1 | 3 | no | combat_chase, combat_influence, path_interruption |
| 35 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | cb29e5facd2142f7 | 7 | 1 | 1 | 7 | no | combat_influence |
| 35 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | e9746cfdcfbccfd5 | 4 | 1 | 1 | 4 | no | combat_influence |
| 35 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | e994e172de8996b3 | 7 | 1 | 1 | 7 | no | combat_influence, path_interruption |
| 35 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | eb78b99404b30058 | 4 | 1 | 1 | 4 | no | path_interruption |
| 35 | leash | 97 | 96195 | 1 | 1044525 | Anger Manifestation | f038fe1b9276b59f | 4 | 1 | 1 | 4 | no | combat_influence, leash_after_combat |
| 35 | patrol | 97 | 96195 | 10 | 1044525 | Pacify | 733aafd6db8f159e | 5 | 1 | 1 | 5 | no | combat_influence |
| 35 | combat chase | 1019 | 17720 | 13 | 1044525 | Robotic Guard Dog | a9f32cdd5d8130c0 | 5 | 1 | 1 | 5 | no | combat_chase, combat_influence, player_influence |
| 35 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 026ae25cd45acd45 | 17 | 1 | 1 | 6 | yes | teleport_or_position_discontinuity |
| 35 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 032e54a08b72c73d | 6 | 1 | 1 | 3 | no | teleport_or_position_discontinuity |
| 35 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 184a6f07332aa71a | 8 | 1 | 1 | 4 | yes | teleport_or_position_discontinuity |
| 35 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 190ab1953278cae2 | 11 | 1 | 1 | 4 | yes | teleport_or_position_discontinuity |
| 35 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 1f84fa8e0b585d92 | 10 | 1 | 1 | 3 | yes | teleport_or_position_discontinuity |
| 35 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 3a4c62263ae3845a | 3 | 1 | 1 | 3 | no | path_interruption |
| 35 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 3c40e5de2c1486aa | 7 | 1 | 1 | 5 | yes | teleport_or_position_discontinuity |
| 35 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 422b87786bdac743 | 8 | 1 | 1 | 4 | yes | teleport_or_position_discontinuity |
| 35 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 4888343bb8c17a6b | 10 | 1 | 1 | 3 | yes | teleport_or_position_discontinuity |
| 35 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 4afc1f65ee957fb4 | 9 | 1 | 1 | 4 | no | teleport_or_position_discontinuity |
| 35 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 64e8f2e5ceedbddd | 4 | 1 | 1 | 3 | no | path_interruption |
| 35 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 661964ef0f325753 | 3 | 1 | 1 | 3 | no | path_interruption |
| 35 | spawn | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 690a3753b44d9da2 | 2 | 1 | 1 | 1 | yes | incomplete_capture, spawn_transient_not_patrol, teleport_or_position_discontinuity |
| 35 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 6c416f0fe720926a | 4 | 1 | 1 | 4 | yes | teleport_or_position_discontinuity |
| 35 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 717b06f61ea52be2 | 7 | 1 | 1 | 3 | yes | teleport_or_position_discontinuity |
| 35 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 78dae436a54a89c3 | 15 | 1 | 1 | 4 | yes | path_interruption |
| 35 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 7bec431cd28759af | 10 | 1 | 1 | 6 | no | teleport_or_position_discontinuity |
| 35 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 850b3326d472dde9 | 8 | 1 | 1 | 4 | yes | teleport_or_position_discontinuity |
| 35 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 8603ee7c7cc2fff9 | 16 | 1 | 1 | 5 | no | teleport_or_position_discontinuity |
| 35 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 8d0e8cca43e97fdf | 9 | 1 | 1 | 4 | yes | teleport_or_position_discontinuity |
| 35 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 9dee17f94536fd37 | 8 | 1 | 1 | 3 | yes | teleport_or_position_discontinuity |
| 35 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | a4594ead5961588d | 13 | 1 | 1 | 4 | yes | teleport_or_position_discontinuity |
| 35 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | a9a26ae9af882bcf | 3 | 1 | 1 | 3 | no | teleport_or_position_discontinuity |
| 35 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | aa69a3b0b6465105 | 7 | 1 | 1 | 5 | yes | teleport_or_position_discontinuity |
| 35 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | bbb69fdfc7011bb0 | 7 | 1 | 1 | 7 | no | teleport_or_position_discontinuity |
| 35 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | c3471e3db4e620de | 7 | 6 | 6 | 1 | no | incomplete_capture, path_interruption |
| 35 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | d0dfd04fcfc7c62f | 4 | 1 | 1 | 4 | yes | teleport_or_position_discontinuity |
| 35 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | d1188aab2d3ca6d4 | 3 | 1 | 1 | 3 | no | teleport_or_position_discontinuity |
| 35 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | e2ef12b494914ef5 | 12 | 1 | 1 | 4 | yes | teleport_or_position_discontinuity |
| 35 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | e8e19a53bc9a5a2b | 11 | 1 | 1 | 4 | yes | teleport_or_position_discontinuity |
| 35 | spawn | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | f8b85a797d102ece | 5 | 4 | 4 | 1 | no | incomplete_capture, path_interruption, spawn_transient_not_patrol |
| 35 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | fab68f3b53d8c79c | 18 | 1 | 1 | 6 | yes | teleport_or_position_discontinuity |
| 35 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | fac517cb4c90cf23 | 5 | 1 | 1 | 4 | no | teleport_or_position_discontinuity |
| 35 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | ff3a4c86d3a875f3 | 9 | 1 | 1 | 3 | no | path_interruption |
| 30 | patrol | unresolved | unresolved | unresolved | unresolved | Malfunctioning Cleaning Robot | 0275ca3a257cb075 | 6 | 1 | 1 | 3 | yes | metadata_missing, teleport_or_position_discontinuity |
| 30 | patrol | unresolved | unresolved | unresolved | unresolved | Malfunctioning Cleaning Robot | 065eabb09cec8a44 | 10 | 1 | 1 | 3 | yes | metadata_missing, teleport_or_position_discontinuity |
| 30 | patrol | unresolved | unresolved | unresolved | unresolved | Malfunctioning Cleaning Robot | 0fd4e69a9754926e | 11 | 1 | 1 | 3 | yes | metadata_missing, teleport_or_position_discontinuity |
| 30 | patrol | unresolved | unresolved | unresolved | unresolved | Malfunctioning Cleaning Robot | 155608c6851fad29 | 3 | 1 | 1 | 3 | yes | metadata_missing, teleport_or_position_discontinuity |
| 30 | patrol | unresolved | unresolved | unresolved | unresolved | Malfunctioning Cleaning Robot | 1af76546eeb8d30a | 8 | 1 | 1 | 3 | yes | metadata_missing, teleport_or_position_discontinuity |
| 30 | patrol | unresolved | unresolved | unresolved | unresolved | Malfunctioning Cleaning Robot | 3771bdec380f41a7 | 3 | 1 | 1 | 3 | yes | metadata_missing, teleport_or_position_discontinuity |
| 30 | patrol | unresolved | unresolved | unresolved | unresolved | Malfunctioning Cleaning Robot | b966b3ecadb0bf3c | 7 | 1 | 1 | 4 | yes | metadata_missing, teleport_or_position_discontinuity |
| 30 | patrol | unresolved | unresolved | unresolved | unresolved | Malfunctioning Cleaning Robot | c3807c43e8ec8466 | 3 | 1 | 1 | 3 | yes | metadata_missing, teleport_or_position_discontinuity |
| 30 | patrol | unresolved | unresolved | unresolved | unresolved | Malfunctioning Cleaning Robot | e698dbc0e9afcb9d | 7 | 1 | 1 | 3 | yes | metadata_missing, teleport_or_position_discontinuity |
| 30 | combat chase | 0 | 26092 | 40 | 1044525 | ICC Peacekeeper | 07075e702102b001 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, path_interruption |
| 30 | flee | 0 | 26092 | 40 | 1044525 | ICC Peacekeeper | 1e6cfae00b7a1240 | 1 | 1 | 1 | 1 | no | combat_flee, combat_influence |
| 30 | patrol | 0 | 26092 | 40 | 1044525 | ICC Peacekeeper | 4c235c94089c0298 | 8 | 1 | 2 | 4 | no | incomplete_capture, teleport_or_position_discontinuity |
| 30 | flee | 0 | 26092 | 40 | 1044525 | ICC Peacekeeper | 6dc8d2c199fb9a5d | 1 | 1 | 1 | 1 | no | combat_flee, combat_influence, path_interruption |
| 30 | patrol | 0 | 26092 | 40 | 1044525 | ICC Peacekeeper | 9a24724dec4b9a80 | 1 | 1 | 1 | 1 | no | combat_influence, teleport_or_position_discontinuity |
| 30 | leash | 0 | 26092 | 40 | 1044525 | ICC Peacekeeper | b398aec4838d98bf | 2 | 1 | 1 | 2 | no | combat_influence, leash_after_combat |
| 30 | patrol | 0 | 26092 | 40 | 1044525 | ICC Peacekeeper | db703a75b43a39fe | 20 | 1 | 5 | 4 | no | incomplete_capture, path_interruption |
| 30 | combat chase | 0 | 26092 | 40 | 1044525 | ICC Peacekeeper | e6bca64602fbc7b8 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, path_interruption, teleport_or_position_discontinuity |
| 30 | leash | 15 | 17662 | 1 | 1044525 | Minibronto | f263b7221a8e7b98 | 1 | 1 | 1 | 1 | no | combat_influence, leash_after_combat |
| 30 | combat chase | 19 | 165188 | 6 | 1044525 | Cedric Harding | 6a6dce0b9359728b | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, player_influence, teleport_or_position_discontinuity |
| 30 | patrol | 25 | 17657 | 1 | 1044525 | Garbage Flea | 83eb997fca0c49e4 | 15 | 1 | 3 | 5 | no | incomplete_capture |
| 30 | combat chase | 25 | 17657 | 1 | 1044525 | Garbage Flea | 857f20ef35733d3b | 2 | 1 | 1 | 2 | no | combat_chase, combat_influence, path_interruption, player_influence, teleport_or_position_discontinuity |
| 30 | patrol | 25 | 17657 | 1 | 1044525 | Garbage Flea | aa004682725accd7 | 100 | 1 | 2 | 6 | no | incomplete_capture |
| 30 | patrol | 25 | 17657 | 1 | 1044525 | Garbage Flea | c91c98e47a07dc7f | 10 | 1 | 2 | 5 | no | incomplete_capture, path_interruption |
| 30 | patrol | 25 | 17657 | 2 | 1044525 | Garbage Flea | 1f56c9f8fbcec58f | 22 | 1 | 3 | 5 | no | incomplete_capture, path_interruption |
| 30 | patrol | 25 | 17657 | 2 | 1044525 | Garbage Flea | e0e0380dc738bdc5 | 36 | 1 | 2 | 5 | no | incomplete_capture, path_interruption |
| 30 | patrol | 25 | 17657 | 5 | 1044525 | Garbage Flea | 099f92c68bbbdfe9 | 1 | 1 | 1 | 1 | no | combat_influence |
| 30 | combat chase | 25 | 17657 | 5 | 1044525 | Garbage Flea | 2d1f8c2de7ff76dd | 2 | 1 | 1 | 2 | no | combat_chase, combat_influence, player_influence, teleport_or_position_discontinuity |
| 30 | leash | 25 | 17657 | 5 | 1044525 | Garbage Flea | 4c89d5c63af60455 | 1 | 1 | 1 | 1 | no | combat_influence, leash_after_combat |
| 30 | combat chase | 25 | 17657 | 5 | 1044525 | Garbage Flea | 524bd3078b7a6800 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, path_interruption, player_influence, teleport_or_position_discontinuity |
| 30 | combat chase | 25 | 17657 | 5 | 1044525 | Garbage Flea | 638fadfde3ac52c3 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, path_interruption, player_influence |
| 30 | combat chase | 25 | 17657 | 5 | 1044525 | Garbage Flea | 9f4979eba20e6f1e | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, path_interruption, player_influence, teleport_or_position_discontinuity |
| 30 | combat chase | 25 | 17657 | 5 | 1044525 | Garbage Flea | b007e4757e25a7be | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, path_interruption, player_influence, teleport_or_position_discontinuity |
| 30 | combat chase | 25 | 17657 | 5 | 1044525 | Garbage Flea | b44942b2a3bdc45d | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, path_interruption, player_influence, teleport_or_position_discontinuity |
| 30 | combat chase | 25 | 17657 | 5 | 1044525 | Garbage Flea | c1178abed698d286 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, path_interruption, player_influence, teleport_or_position_discontinuity |
| 30 | flee | 25 | 17657 | 5 | 1044525 | Garbage Flea | f081850934749b2f | 1 | 1 | 1 | 1 | no | combat_flee, combat_influence, player_influence |
| 30 | combat chase | 25 | 17657 | 5 | 1044525 | Garbage Flea | fab09e4239b7227a | 2 | 1 | 1 | 2 | no | combat_chase, combat_influence, path_interruption, player_influence, teleport_or_position_discontinuity |
| 30 | combat chase | 25 | 17657 | 6 | 1044525 | Garbage Flea | 0181accccb7d8b07 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, player_influence, teleport_or_position_discontinuity |
| 30 | combat chase | 25 | 17657 | 6 | 1044525 | Garbage Flea | 09474b1205bbc27d | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, path_interruption, player_influence, teleport_or_position_discontinuity |
| 30 | combat chase | 25 | 17657 | 6 | 1044525 | Garbage Flea | 204d7061476d5a34 | 2 | 1 | 1 | 2 | no | combat_chase, combat_influence, player_influence |
| 30 | combat chase | 25 | 17657 | 6 | 1044525 | Garbage Flea | 420233f7e96279dd | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, player_influence, teleport_or_position_discontinuity |
| 30 | combat chase | 25 | 17657 | 6 | 1044525 | Garbage Flea | 44e5eb8627c09ebd | 2 | 1 | 1 | 2 | no | combat_chase, combat_influence, path_interruption, player_influence, teleport_or_position_discontinuity |
| 30 | combat chase | 25 | 17657 | 6 | 1044525 | Garbage Flea | 97731810f2ed66c2 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, player_influence |
| 30 | flee | 25 | 17657 | 6 | 1044525 | Garbage Flea | 9fcbace662ebbd75 | 1 | 1 | 1 | 1 | no | combat_flee, combat_influence, player_influence |
| 30 | flee | 25 | 17657 | 6 | 1044525 | Garbage Flea | c48da25c27ff8b0d | 1 | 1 | 1 | 1 | no | combat_flee, combat_influence, player_influence |
| 30 | combat chase | 25 | 17657 | 6 | 1044525 | Garbage Flea | cbc21b23bb8ca6a7 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, player_influence, teleport_or_position_discontinuity |
| 30 | combat chase | 25 | 17657 | 6 | 1044525 | Garbage Flea | e4333fba5150b2cd | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, player_influence, teleport_or_position_discontinuity |
| 30 | combat chase | 25 | 17657 | 6 | 1044525 | Garbage Flea | e7fc5ac5c30e5149 | 2 | 1 | 1 | 2 | no | combat_chase, combat_influence, player_influence, teleport_or_position_discontinuity |
| 30 | combat chase | 25 | 17657 | 6 | 1044525 | Garbage Flea | f113941b015b8df0 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, path_interruption, player_influence, teleport_or_position_discontinuity |
| 30 | combat chase | 25 | 17657 | 7 | 1044525 | Mutated Garbage Flea | 5279a3f6a6dab2ef | 2 | 1 | 1 | 2 | no | combat_chase, combat_influence, path_interruption, player_influence, teleport_or_position_discontinuity |
| 30 | combat chase | 25 | 17657 | 7 | 1044525 | Mutated Garbage Flea | 5d145db0c3874f82 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, player_influence, teleport_or_position_discontinuity |
| 30 | combat chase | 25 | 17657 | 7 | 1044525 | Mutated Garbage Flea | 807f35cd65d33dc8 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, path_interruption |
| 30 | combat chase | 25 | 17657 | 7 | 1044525 | Mutated Garbage Flea | 8ce02c89670a3260 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, player_influence, teleport_or_position_discontinuity |
| 30 | spawn | 25 | 17657 | 7 | 1044525 | Mutated Garbage Flea | 9351cbb828d4264c | 1 | 1 | 1 | 1 | no | path_interruption, spawn_transient_not_patrol |
| 30 | combat chase | 42 | 30360 | 8 | 1044525 | Angry Minibull | 4cb56f5f130c8046 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, player_influence, teleport_or_position_discontinuity |
| 30 | combat chase | 42 | 30360 | 8 | 1044525 | Angry Minibull | b1682229fe9b1d81 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, player_influence, teleport_or_position_discontinuity |
| 30 | combat chase | 42 | 30360 | 8 | 1044525 | Angry Minibull | ce4f3fddb9ea683b | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, player_influence, teleport_or_position_discontinuity |
| 30 | combat chase | 42 | 30360 | 8 | 1044525 | Angry Minibull | f07c095c50115954 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, player_influence, teleport_or_position_discontinuity |
| 30 | combat chase | 42 | 30360 | 9 | 1044525 | Angry Minibull | b741cfa46bc33347 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, player_influence, teleport_or_position_discontinuity |
| 30 | combat chase | 42 | 30360 | 10 | 1044525 | Angry Minibull | 1b9c36dc9e0ab2b1 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, player_influence |
| 30 | combat chase | 42 | 30360 | 10 | 1044525 | Angry Minibull | 7e02b89c2ec86ec5 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence |
| 30 | combat chase | 42 | 30360 | 12 | 1044525 | Angry Minibull | 24f4956e6358b436 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, player_influence, teleport_or_position_discontinuity |
| 30 | combat chase | 42 | 30360 | 12 | 1044525 | Angry Minibull | 417c94251c9309ad | 2 | 1 | 1 | 2 | no | combat_chase, combat_influence |
| 30 | combat chase | 42 | 30360 | 12 | 1044525 | Angry Minibull | 4d46b63c38afb8dd | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, path_interruption, player_influence |
| 30 | combat chase | 42 | 30360 | 12 | 1044525 | Angry Minibull | 5be922fa9e267e46 | 2 | 1 | 1 | 2 | no | combat_chase, combat_influence, teleport_or_position_discontinuity |
| 30 | flee | 42 | 30360 | 12 | 1044525 | Angry Minibull | 9793a53b7db2f380 | 1 | 1 | 1 | 1 | no | combat_flee, combat_influence, path_interruption, player_influence |
| 30 | combat chase | 42 | 30360 | 12 | 1044525 | Angry Minibull | a46875d17fb639ff | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, path_interruption |
| 30 | combat chase | 42 | 30360 | 12 | 1044525 | Angry Minibull | be429c7dda1c0ed1 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, path_interruption, player_influence, teleport_or_position_discontinuity |
| 30 | combat chase | 42 | 30360 | 13 | 1044525 | Angry Minibull | 23128206d0c48e58 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, teleport_or_position_discontinuity |
| 30 | combat chase | 42 | 30360 | 13 | 1044525 | Angry Minibull | 74edc702b2ce1b18 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, player_influence, teleport_or_position_discontinuity |
| 30 | combat chase | 42 | 30360 | 13 | 1044525 | Angry Minibull | aa73fa1a15af7275 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, player_influence, teleport_or_position_discontinuity |
| 30 | combat chase | 53 | 30365 | 5 | 1044525 | Desert Reet | 1b3c221cfa263ab5 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, path_interruption, player_influence |
| 30 | combat chase | 53 | 30365 | 5 | 1044525 | Desert Reet | 33a3dc101a9ce682 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, path_interruption, player_influence |
| 30 | combat chase | 53 | 30365 | 5 | 1044525 | Desert Reet | d13ac4c4bd695d70 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, player_influence, teleport_or_position_discontinuity |
| 30 | combat chase | 53 | 30365 | 6 | 1044525 | Desert Reet | 2f9482fe81422dd9 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, player_influence, teleport_or_position_discontinuity |
| 30 | combat chase | 53 | 30365 | 6 | 1044525 | Desert Reet | aca3bdc20ab3d6fc | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, player_influence |
| 30 | combat chase | 53 | 30365 | 6 | 1044525 | Desert Reet | e74c1420f31a161e | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, player_influence, teleport_or_position_discontinuity |
| 30 | combat chase | 55 | 17687 | 5 | 1044525 | Rollerrat | 14b2b7ea03000026 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, player_influence, teleport_or_position_discontinuity |
| 30 | combat chase | 55 | 17687 | 5 | 1044525 | Rollerrat | 3a8a4e849fb669f9 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, player_influence, teleport_or_position_discontinuity |
| 30 | combat chase | 55 | 17687 | 5 | 1044525 | Rollerrat | 53413799f6e8b314 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence |
| 30 | combat chase | 55 | 17687 | 5 | 1044525 | Rollerrat | 595b4c70c9620e7c | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, player_influence, teleport_or_position_discontinuity |
| 30 | combat chase | 55 | 17687 | 5 | 1044525 | Rollerrat | 67e133e409b0973c | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, player_influence, teleport_or_position_discontinuity |
| 30 | combat chase | 55 | 17687 | 5 | 1044525 | Rollerrat | 6e24b6fed9fe46d2 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, player_influence, teleport_or_position_discontinuity |
| 30 | combat chase | 55 | 17687 | 5 | 1044525 | Rollerrat | 81444eac4e174953 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, player_influence, teleport_or_position_discontinuity |
| 30 | combat chase | 55 | 17687 | 5 | 1044525 | Rollerrat | 95fcd6bf773dbd17 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, player_influence, teleport_or_position_discontinuity |
| 30 | combat chase | 55 | 17687 | 5 | 1044525 | Rollerrat | c458670d64c6703b | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, player_influence, teleport_or_position_discontinuity |
| 30 | combat chase | 55 | 17687 | 5 | 1044525 | Rollerrat | d9c384da1c9bbe15 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, player_influence, teleport_or_position_discontinuity |
| 30 | combat chase | 55 | 17687 | 5 | 1044525 | Rollerrat | eda6d5222ab7a75b | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, player_influence, teleport_or_position_discontinuity |
| 30 | combat chase | 55 | 17687 | 5 | 1044525 | Rollerrat | ffc30937aafa11fd | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, player_influence, teleport_or_position_discontinuity |
| 30 | combat chase | 55 | 17687 | 6 | 1044525 | Rollerrat | 020ef858fe29549e | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, player_influence, teleport_or_position_discontinuity |
| 30 | combat chase | 55 | 17687 | 6 | 1044525 | Rollerrat | 0aa89274aff07667 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, player_influence, teleport_or_position_discontinuity |
| 30 | combat chase | 55 | 17687 | 6 | 1044525 | Rollerrat | 128bfcaedaff5946 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, player_influence, teleport_or_position_discontinuity |
| 30 | flee | 55 | 17687 | 6 | 1044525 | Rollerrat | 14afe9f54cd5ccb2 | 1 | 1 | 1 | 1 | no | combat_flee, combat_influence, player_influence |
| 30 | combat chase | 55 | 17687 | 6 | 1044525 | Rollerrat | 2300b2d528bd56cc | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, player_influence, teleport_or_position_discontinuity |
| 30 | combat chase | 55 | 17687 | 6 | 1044525 | Rollerrat | 3f53fc3d242d05a5 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, player_influence, teleport_or_position_discontinuity |
| 30 | combat chase | 55 | 17687 | 6 | 1044525 | Rollerrat | 442d24d8b2eaa7e3 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, player_influence |
| 30 | combat chase | 55 | 17687 | 6 | 1044525 | Rollerrat | 5083c32a3a18c45f | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, player_influence, teleport_or_position_discontinuity |
| 30 | combat chase | 55 | 17687 | 6 | 1044525 | Rollerrat | 5a4e07406e145914 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, player_influence |
| 30 | combat chase | 55 | 17687 | 6 | 1044525 | Rollerrat | 6124b8c0639b7fd2 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, player_influence, teleport_or_position_discontinuity |
| 30 | combat chase | 55 | 17687 | 6 | 1044525 | Rollerrat | 9f86cb33664b5a40 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, player_influence, teleport_or_position_discontinuity |
| 30 | combat chase | 55 | 17687 | 6 | 1044525 | Rollerrat | a42e43bf469d0bd5 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, player_influence, teleport_or_position_discontinuity |
| 30 | combat chase | 55 | 17687 | 6 | 1044525 | Rollerrat | bcee91583eda4037 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, player_influence, teleport_or_position_discontinuity |
| 30 | combat chase | 55 | 17687 | 6 | 1044525 | Rollerrat | c0259b450355773a | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, player_influence, teleport_or_position_discontinuity |
| 30 | combat chase | 55 | 17687 | 6 | 1044525 | Rollerrat | c8bcbeb9166997ff | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, path_interruption, player_influence, teleport_or_position_discontinuity |
| 30 | combat chase | 55 | 17687 | 6 | 1044525 | Rollerrat | cc6558c2c43a782c | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, path_interruption, player_influence, teleport_or_position_discontinuity |
| 30 | combat chase | 55 | 17687 | 6 | 1044525 | Rollerrat | e79d7697ff7e4a3b | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, player_influence, teleport_or_position_discontinuity |
| 30 | combat chase | 55 | 17687 | 6 | 1044525 | Rollerrat | ec65632acfa07f4f | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, player_influence, teleport_or_position_discontinuity |
| 30 | combat chase | 55 | 17687 | 6 | 1044525 | Rollerrat | f65929d824283f6e | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, player_influence, teleport_or_position_discontinuity |
| 30 | combat chase | 55 | 17687 | 7 | 1044525 | Gnarl the Roller | 69f32387160e41b0 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, path_interruption, player_influence, teleport_or_position_discontinuity |
| 30 | patrol | 55 | 17687 | 7 | 1044525 | Gnarl the Roller | c47a2f028915552a | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | flee | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | 000f9069c44c1c23 | 1 | 1 | 1 | 1 | no | combat_flee, combat_influence, path_interruption |
| 30 | patrol | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | 00a870d41374f0cb | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | leash | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | 03ae0969a5064bfd | 1 | 1 | 1 | 1 | no | combat_influence, leash_after_combat, path_interruption |
| 30 | combat chase | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | 03fc69d4a2fffc92 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, path_interruption, teleport_or_position_discontinuity |
| 30 | patrol | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | 065b2c57c1e0a985 | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | patrol | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | 071a07d6ee949dfc | 2 | 1 | 1 | 2 | no | combat_influence |
| 30 | combat chase | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | 09c71752d4a1d2aa | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, path_interruption |
| 30 | patrol | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | 0a36e42214fbc553 | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | patrol | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | 0b5d2321959bffba | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | patrol | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | 11a5674948fcdc8b | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | leash | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | 11e3862856ba1a38 | 1 | 1 | 1 | 1 | no | combat_influence, leash_after_combat |
| 30 | patrol | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | 1306e82b273ccd24 | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | leash | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | 139c3643716e5eb4 | 1 | 1 | 1 | 1 | no | combat_influence, leash_after_combat |
| 30 | patrol | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | 13d248a096a0b6ce | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | patrol | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | 2066f1852b2f70e3 | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | combat chase | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | 20eaa9b734320fd0 | 2 | 1 | 1 | 2 | no | combat_chase, combat_influence, path_interruption |
| 30 | combat chase | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | 20f16be160513a1d | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence |
| 30 | combat chase | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | 2231e10fecc6ec7c | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, path_interruption |
| 30 | combat chase | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | 2314ea2607981e81 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence |
| 30 | combat chase | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | 23c0575b1365d71a | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, path_interruption, teleport_or_position_discontinuity |
| 30 | patrol | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | 25845d825a26e55a | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | combat chase | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | 25b48ca1b21b5c7d | 2 | 1 | 1 | 2 | no | combat_chase, combat_influence |
| 30 | patrol | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | 27a0a83afd04b446 | 1 | 1 | 1 | 1 | no | combat_influence, path_interruption |
| 30 | patrol | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | 2adf1d0f0cb89c6c | 1 | 1 | 1 | 1 | no | combat_influence |
| 30 | leash | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | 2c049aede6ff59a2 | 1 | 1 | 1 | 1 | no | combat_influence, leash_after_combat |
| 30 | patrol | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | 2e1ac28f53594ae4 | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | patrol | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | 2f8b00f29441d3e2 | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | patrol | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | 321fc9378f3ac47a | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | patrol | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | 32c322b5ffb087fd | 2 | 1 | 1 | 2 | no | combat_influence |
| 30 | patrol | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | 37699db1d70f89ee | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | combat chase | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | 3e270469b8b2ad35 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, path_interruption, teleport_or_position_discontinuity |
| 30 | patrol | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | 3ed3b07ca2c3ccdf | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | combat chase | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | 3f968d3874f28356 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence |
| 30 | combat chase | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | 408bc0b1ab3b56f4 | 2 | 1 | 1 | 2 | no | combat_chase, combat_influence, path_interruption, teleport_or_position_discontinuity |
| 30 | flee | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | 43ebe1a710a6092b | 2 | 1 | 1 | 2 | no | combat_flee, combat_influence, path_interruption |
| 30 | leash | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | 459e4d9e3d41042d | 1 | 1 | 1 | 1 | no | combat_influence, leash_after_combat |
| 30 | combat chase | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | 4879764340b7d17f | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, teleport_or_position_discontinuity |
| 30 | patrol | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | 48bbf9dae5c3d39d | 1 | 1 | 1 | 1 | no | path_interruption, teleport_or_position_discontinuity |
| 30 | patrol | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | 4aebd6231b9c1722 | 2 | 1 | 1 | 2 | no | combat_influence |
| 30 | combat chase | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | 4cfca04ea4229baa | 2 | 1 | 1 | 2 | no | combat_chase, combat_influence |
| 30 | flee | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | 50f617fffd9e03a4 | 1 | 1 | 1 | 1 | no | combat_flee, combat_influence, path_interruption |
| 30 | patrol | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | 5138ed9308f9c807 | 1 | 1 | 1 | 1 | no | path_interruption, teleport_or_position_discontinuity |
| 30 | combat chase | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | 566eb99fa4b03ef1 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, path_interruption, teleport_or_position_discontinuity |
| 30 | patrol | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | 61bef26d3df87f7a | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | combat chase | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | 61eb94c045f8dbfc | 2 | 1 | 1 | 2 | no | combat_chase, combat_influence, path_interruption, teleport_or_position_discontinuity |
| 30 | patrol | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | 629124367f1646d0 | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | flee | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | 66e7091c543c0f47 | 1 | 1 | 1 | 1 | no | combat_flee, combat_influence, path_interruption |
| 30 | patrol | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | 69345f8f6f705921 | 2 | 1 | 1 | 2 | no | combat_influence |
| 30 | patrol | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | 6aaffc346087d0cc | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | patrol | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | 6d72401c2f00a2b7 | 1 | 1 | 1 | 1 | no | combat_influence, path_interruption |
| 30 | patrol | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | 6d93146963691bd6 | 1 | 1 | 1 | 1 | no | combat_influence, path_interruption |
| 30 | combat chase | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | 6e663b6985f6e4b5 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, path_interruption, teleport_or_position_discontinuity |
| 30 | patrol | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | 725535129c78d7e8 | 1 | 1 | 1 | 1 | no | combat_influence |
| 30 | combat chase | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | 73ae6a95e3017280 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence |
| 30 | leash | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | 748f8639e315c37b | 1 | 1 | 1 | 1 | no | combat_influence, leash_after_combat |
| 30 | patrol | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | 7579e88223f35b28 | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | patrol | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | 7625750e6238ad7c | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | combat chase | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | 7963ab174b596a7f | 2 | 1 | 1 | 2 | no | combat_chase, combat_influence |
| 30 | combat chase | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | 7e697e06276e521f | 2 | 1 | 1 | 2 | no | combat_chase, combat_influence, path_interruption, teleport_or_position_discontinuity |
| 30 | combat chase | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | 7f434b98bdef0fca | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, path_interruption, teleport_or_position_discontinuity |
| 30 | patrol | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | 7fb0ac8b453fd5a5 | 1 | 1 | 1 | 1 | no | teleport_or_position_discontinuity |
| 30 | combat chase | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | 867f0ac4e938e7ec | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, path_interruption, teleport_or_position_discontinuity |
| 30 | patrol | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | 8991e58454d35422 | 1 | 1 | 1 | 1 | no | combat_influence, path_interruption |
| 30 | combat chase | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | 8be402904265f263 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, path_interruption |
| 30 | combat chase | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | 901af2d70658ca5c | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence |
| 30 | patrol | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | 90841aa4de9c48bf | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | patrol | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | 9453ee87caae86e0 | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | patrol | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | 9711360e4973aa6b | 1 | 1 | 1 | 1 | no | combat_influence, path_interruption |
| 30 | patrol | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | 989136bf2731b8a1 | 1 | 1 | 1 | 1 | no | combat_influence |
| 30 | leash | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | 98eba9e6c1275969 | 1 | 1 | 1 | 1 | no | combat_influence, leash_after_combat |
| 30 | leash | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | 9d1aebf0619c2510 | 1 | 1 | 1 | 1 | no | combat_influence, leash_after_combat |
| 30 | patrol | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | 9e5e5fea76ca5f64 | 1 | 1 | 1 | 1 | no | path_interruption, teleport_or_position_discontinuity |
| 30 | patrol | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | a044f16ad55cc73a | 1 | 1 | 1 | 1 | no | combat_influence, path_interruption |
| 30 | patrol | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | a125f2fc565c9f23 | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | patrol | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | a347e6ef4e2f963b | 1 | 1 | 1 | 1 | no | combat_influence, path_interruption |
| 30 | patrol | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | a4507a9cd6c3f0e8 | 1 | 1 | 1 | 1 | no | combat_influence, path_interruption |
| 30 | flee | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | a52f078e0756033f | 1 | 1 | 1 | 1 | no | combat_flee, combat_influence |
| 30 | leash | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | a60df15a5dba1f2f | 1 | 1 | 1 | 1 | no | combat_influence, leash_after_combat |
| 30 | patrol | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | a7b96b84501318bd | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | leash | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | a878a1c0b5e70302 | 1 | 1 | 1 | 1 | no | combat_influence, leash_after_combat |
| 30 | combat chase | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | ac41423c1a66cd3a | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, teleport_or_position_discontinuity |
| 30 | patrol | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | ac607a07d2c7718c | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | combat chase | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | acddede4da040c69 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, path_interruption |
| 30 | combat chase | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | b06ec43d7cc58b7f | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, path_interruption |
| 30 | patrol | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | b19168456c0624e7 | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | combat chase | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | b21692db962f4319 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence |
| 30 | patrol | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | b79e40d2cdbfe4c4 | 1 | 1 | 1 | 1 | no | combat_influence |
| 30 | patrol | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | bc3d706e57887160 | 2 | 1 | 1 | 2 | no | path_interruption |
| 30 | combat chase | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | bf3c143227292177 | 2 | 1 | 1 | 2 | no | combat_chase, combat_influence |
| 30 | patrol | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | c0580a2ace13df36 | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | patrol | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | c2bedc600fb338a0 | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | patrol | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | c2f45f1bdf52f35c | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | combat chase | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | c38bac162c1f1a11 | 2 | 1 | 1 | 2 | no | combat_chase, combat_influence, path_interruption, teleport_or_position_discontinuity |
| 30 | combat chase | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | c62dbaa386f4ad8b | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, path_interruption, teleport_or_position_discontinuity |
| 30 | patrol | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | c8d99c4a97eb125e | 1 | 1 | 1 | 1 | no | combat_influence |
| 30 | patrol | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | cc91f6b3c5875fc4 | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | patrol | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | cd302ecbd371d457 | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | leash | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | cedcf43aec45391e | 1 | 1 | 1 | 1 | no | combat_influence, leash_after_combat |
| 30 | flee | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | cf80b7a7ad21d7b4 | 1 | 1 | 1 | 1 | no | combat_flee, combat_influence, path_interruption |
| 30 | patrol | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | cfe1c48fe884c13c | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | patrol | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | d04ebe4813bc293d | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | patrol | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | d439cb616c399b07 | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | combat chase | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | d4e515946568df80 | 2 | 1 | 1 | 2 | no | combat_chase, combat_influence, path_interruption, teleport_or_position_discontinuity |
| 30 | flee | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | d5a76da4418ef158 | 1 | 1 | 1 | 1 | no | combat_flee, combat_influence, path_interruption |
| 30 | patrol | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | d5b38aba2182cc7b | 1 | 1 | 1 | 1 | no | combat_influence, path_interruption |
| 30 | combat chase | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | dc4bcc11967e7642 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, path_interruption, teleport_or_position_discontinuity |
| 30 | patrol | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | e10df1c1dd4a3ae4 | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | patrol | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | e14eb3b8528a8d7b | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | combat chase | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | e1b45d07cbea01ab | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, teleport_or_position_discontinuity |
| 30 | patrol | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | e4753e5c154ae367 | 1 | 1 | 1 | 1 | no | combat_influence |
| 30 | leash | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | e68f92d894deeeb2 | 1 | 1 | 1 | 1 | no | combat_influence, leash_after_combat |
| 30 | combat chase | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | e959e6553f1183dd | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, path_interruption, teleport_or_position_discontinuity |
| 30 | patrol | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | e95a630795577311 | 2 | 1 | 1 | 2 | no | combat_influence |
| 30 | patrol | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | eb6527001bedf997 | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | patrol | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | ecf015d05655c121 | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | patrol | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | f055dfef83bf4bdd | 2 | 1 | 1 | 2 | no | combat_influence |
| 30 | flee | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | f062bf868b8d52e0 | 1 | 1 | 1 | 1 | no | combat_flee, combat_influence |
| 30 | patrol | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | f4bea33be4f5b2b7 | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | patrol | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | f9f15004dd3f1b61 | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | patrol | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | fefdcb0855a6f5e8 | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | leash | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 003b1253b3b4a415 | 1 | 1 | 1 | 1 | no | combat_influence, leash_after_combat |
| 30 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 0067cecf7923f653 | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | combat chase | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 013fdd06347c2cc4 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, path_interruption, teleport_or_position_discontinuity |
| 30 | flee | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 023422e78c294c62 | 1 | 1 | 1 | 1 | no | combat_flee, combat_influence, path_interruption |
| 30 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 0235a032e4aa13d9 | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | combat chase | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 02db4e4366d8fd1c | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, path_interruption, teleport_or_position_discontinuity |
| 30 | combat chase | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 036c78ca8b5c9f02 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, path_interruption, teleport_or_position_discontinuity |
| 30 | combat chase | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 04e157ef75b5f5f6 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, path_interruption |
| 30 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 05862f1f8f14cf7a | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | leash | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 05d7c9fd54b9de55 | 2 | 1 | 1 | 2 | no | combat_influence, leash_after_combat |
| 30 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 070dc53eb98cdf62 | 2 | 1 | 1 | 2 | no | combat_influence |
| 30 | combat chase | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 09af2931e34f0664 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence |
| 30 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 0b79f290a675d36e | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | combat chase | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 0ce30b148d8ab18c | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence |
| 30 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 0d6043bd0c3b8098 | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | combat chase | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 0f7c65f3c023fa87 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, teleport_or_position_discontinuity |
| 30 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 107c18ac0a772d26 | 1 | 1 | 1 | 1 | no | combat_influence |
| 30 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 12bb295338dde439 | 1 | 1 | 1 | 1 | no | combat_influence |
| 30 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 12e5c23d9b2f6027 | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | combat chase | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 13b1605cdfa47794 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, teleport_or_position_discontinuity |
| 30 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 18794e330d6699a6 | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 18c05e861fd24281 | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | combat chase | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 1a24328109702b3f | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, teleport_or_position_discontinuity |
| 30 | combat chase | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 1a6ca96d8019e391 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, path_interruption, teleport_or_position_discontinuity |
| 30 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 1d7fdc057fcd7420 | 1 | 1 | 1 | 1 | no | combat_influence, path_interruption |
| 30 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 1f1b7df5cd4ea48f | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 1fa0632df9f4bb98 | 1 | 1 | 1 | 1 | no | combat_influence, path_interruption |
| 30 | combat chase | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 205ac462221b3034 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, path_interruption, teleport_or_position_discontinuity |
| 30 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 2193837aa99390f3 | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | flee | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 21e03169168382ad | 1 | 1 | 1 | 1 | no | combat_flee, combat_influence |
| 30 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 232acb82e53eebad | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 23cd3bcd4a056c8e | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | combat chase | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 292108f4f956abb5 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, path_interruption, teleport_or_position_discontinuity |
| 30 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 2bd5c367f014d754 | 2 | 1 | 1 | 2 | no | combat_influence |
| 30 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 2bef5ead0e2bfbdd | 1 | 1 | 1 | 1 | no | combat_influence, path_interruption |
| 30 | combat chase | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 2cc4a68f5aec8b1d | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence |
| 30 | combat chase | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 317e4b91185acea1 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, path_interruption, teleport_or_position_discontinuity |
| 30 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 38224f10abbc5b25 | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 3953f04b9f13467d | 2 | 1 | 1 | 2 | no | combat_influence |
| 30 | combat chase | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 3a3ac755fae2da44 | 2 | 1 | 1 | 2 | no | combat_chase, combat_influence, path_interruption, teleport_or_position_discontinuity |
| 30 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 3a8c63cb33eccfd8 | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 3beedd04b98a4986 | 1 | 1 | 1 | 1 | no | combat_influence, path_interruption |
| 30 | combat chase | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 3d1edcd9b8e40922 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, path_interruption, teleport_or_position_discontinuity |
| 30 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 3d8126d29ed5fc71 | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 3e6e7c8b797d6589 | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | combat chase | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 41f82d37860902ab | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence |
| 30 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 4256d56635284d9d | 1 | 1 | 1 | 1 | no | combat_influence, path_interruption |
| 30 | combat chase | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 482cc5687d2a82c0 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, path_interruption |
| 30 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 4919e1638af24bc5 | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | leash | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 493a05a6ee2646af | 1 | 1 | 1 | 1 | no | combat_influence, leash_after_combat |
| 30 | leash | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 4b8cdbd688e7da4e | 1 | 1 | 1 | 1 | no | combat_influence, leash_after_combat, path_interruption |
| 30 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 4bffa80466b9399d | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 4ff680e697c50395 | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 50638319afa1913f | 1 | 1 | 1 | 1 | no | combat_influence |
| 30 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 51e34e5677e16294 | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 530593e91bfe072d | 1 | 1 | 1 | 1 | no | combat_influence, path_interruption |
| 30 | combat chase | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 53957564f1a92cdf | 2 | 1 | 1 | 2 | no | combat_chase, combat_influence, path_interruption, teleport_or_position_discontinuity |
| 30 | leash | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 55bb243ad18a30ac | 1 | 1 | 1 | 1 | no | combat_influence, leash_after_combat |
| 30 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 55ca6d55d5ba91ac | 2 | 1 | 1 | 2 | no | combat_influence |
| 30 | leash | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 57abe2c641cb0d4b | 1 | 1 | 1 | 1 | no | combat_influence, leash_after_combat |
| 30 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 57dd60656791e575 | 1 | 1 | 1 | 1 | no | path_interruption, teleport_or_position_discontinuity |
| 30 | combat chase | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 5938ce038f010db8 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, path_interruption, teleport_or_position_discontinuity |
| 30 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 5b6af0533da7a00b | 1 | 1 | 1 | 1 | no | combat_influence |
| 30 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 5c27589fc24147fc | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | combat chase | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 5ce56cca046bdd2a | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, path_interruption, teleport_or_position_discontinuity |
| 30 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 5d947f177e327d99 | 1 | 1 | 1 | 1 | no | combat_influence, path_interruption |
| 30 | combat chase | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 601b08aa68297833 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, path_interruption |
| 30 | combat chase | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 63426728fea6efe9 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, path_interruption, teleport_or_position_discontinuity |
| 30 | combat chase | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 64538850d597463b | 2 | 1 | 1 | 2 | no | combat_chase, combat_influence, path_interruption, teleport_or_position_discontinuity |
| 30 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 6906a4099b2c99bc | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | combat chase | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 6da34486e8bd1d0d | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, path_interruption |
| 30 | combat chase | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 6dd0c8f4db2db2bd | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, path_interruption, teleport_or_position_discontinuity |
| 30 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 6de141a298c8eabf | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | combat chase | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 7007f5fc65938ae1 | 2 | 1 | 1 | 1 | no | combat_chase, combat_influence |
| 30 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 714306f8fd4f5c9b | 2 | 1 | 1 | 2 | no | combat_influence |
| 30 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 717794bc44fff887 | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | combat chase | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 74fbfeb5b09a1637 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, path_interruption, teleport_or_position_discontinuity |
| 30 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 75635b63be9a0212 | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | combat chase | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 79a68b2e178a5c6d | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence |
| 30 | combat chase | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 7ba63754ab059a8f | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, path_interruption, teleport_or_position_discontinuity |
| 30 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 7bea46523c66feec | 1 | 1 | 1 | 1 | no | combat_influence, path_interruption |
| 30 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 7c9abb55540db38d | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 7d647e9c427b88ba | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | combat chase | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 80773e1833ef5177 | 2 | 1 | 1 | 2 | no | combat_chase, combat_influence, teleport_or_position_discontinuity |
| 30 | combat chase | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 836b0c60d073f1ce | 2 | 1 | 1 | 2 | no | combat_chase, combat_influence, path_interruption |
| 30 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 838a8db5f0338eda | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 8445065564288ff8 | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | combat chase | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 84a9e28643c33c98 | 2 | 1 | 1 | 2 | no | combat_chase, combat_influence, teleport_or_position_discontinuity |
| 30 | combat chase | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 86014f5d6acf3d8d | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, teleport_or_position_discontinuity |
| 30 | combat chase | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 883fee6202a5aed2 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, path_interruption, teleport_or_position_discontinuity |
| 30 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 88a1581535d2cdf5 | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | flee | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 8a3c342d170a60d3 | 1 | 1 | 1 | 1 | no | combat_flee, combat_influence, path_interruption |
| 30 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 8b43883cecb098d0 | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 8cacb91e67040a68 | 1 | 1 | 1 | 1 | no | combat_influence, path_interruption |
| 30 | leash | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 8d017dc9780ca293 | 1 | 1 | 1 | 1 | no | combat_influence, leash_after_combat |
| 30 | flee | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 8e7f72115857f618 | 1 | 1 | 1 | 1 | no | combat_flee, combat_influence, path_interruption |
| 30 | leash | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 903fbee9cc563f01 | 1 | 1 | 1 | 1 | no | combat_influence, leash_after_combat |
| 30 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 9294fde7a6a5e432 | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 934ebc52349226ff | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | combat chase | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 93ff45d5b78976a8 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence |
| 30 | combat chase | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 95ce07821212dcd6 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, path_interruption, teleport_or_position_discontinuity |
| 30 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 981de9fa5b64fe2a | 1 | 1 | 1 | 1 | no | combat_influence |
| 30 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 9b5c5f159b5061d1 | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | leash | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 9d6d83828f2b5303 | 1 | 1 | 1 | 1 | no | combat_influence, leash_after_combat |
| 30 | flee | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | a2132d53ec08c484 | 1 | 1 | 1 | 1 | no | combat_flee, combat_influence |
| 30 | combat chase | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | a2abf25c7b265b3a | 2 | 1 | 1 | 2 | no | combat_chase, combat_influence, path_interruption, teleport_or_position_discontinuity |
| 30 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | a736c1a1d92c5097 | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | flee | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | a8803161a2ea8287 | 1 | 1 | 1 | 1 | no | combat_flee, combat_influence, path_interruption |
| 30 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | a8d7dec200fac604 | 1 | 1 | 1 | 1 | no | combat_influence, path_interruption |
| 30 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | a926cacde3c05bcf | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | ae7403d455d460b9 | 1 | 1 | 1 | 1 | no | path_interruption, teleport_or_position_discontinuity |
| 30 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | afe90962b7f80d2e | 1 | 1 | 1 | 1 | no | combat_influence |
| 30 | combat chase | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | b1a6f0c76026573b | 2 | 1 | 1 | 2 | no | combat_chase, combat_influence, teleport_or_position_discontinuity |
| 30 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | b2d2b04cd8dc1f81 | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | b692c35a4802ed7c | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | flee | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | b7b6cff6158ee3fc | 1 | 1 | 1 | 1 | no | combat_flee, combat_influence, path_interruption |
| 30 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | b818671f4be1c02a | 1 | 1 | 1 | 1 | no | combat_influence, path_interruption |
| 30 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | ba9dd94d8f4ecc68 | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | bf295e36c8c45b9b | 2 | 1 | 1 | 2 | no | path_interruption |
| 30 | combat chase | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | c1a4461ad5d627b8 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, path_interruption, teleport_or_position_discontinuity |
| 30 | combat chase | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | c431e3cd0ec465a7 | 2 | 1 | 1 | 2 | no | combat_chase, combat_influence, teleport_or_position_discontinuity |
| 30 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | c72e1e1e8dcd8710 | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | cae0d12f2e6c0946 | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | combat chase | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | d09c910b48f72074 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, path_interruption |
| 30 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | d25847d11cbece41 | 1 | 1 | 1 | 1 | no | teleport_or_position_discontinuity |
| 30 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | d2cc0338604c3309 | 1 | 1 | 1 | 1 | no | combat_influence |
| 30 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | d54334c5352c4fe9 | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | d5a931d6d14c86ac | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | d5d5dd775e22748d | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | combat chase | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | d6b508415e60e158 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, teleport_or_position_discontinuity |
| 30 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | db6f0784b35dfb23 | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | ddd447f934176728 | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | leash | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | deb6dc5ff15549c1 | 1 | 1 | 1 | 1 | no | combat_influence, leash_after_combat, path_interruption |
| 30 | leash | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | e0c1c788bbdc8096 | 1 | 1 | 1 | 1 | no | combat_influence, leash_after_combat |
| 30 | combat chase | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | e2cc9a5383ec4ca9 | 2 | 1 | 1 | 2 | no | combat_chase, combat_influence, path_interruption, teleport_or_position_discontinuity |
| 30 | leash | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | e588d5de89e92636 | 1 | 1 | 1 | 1 | no | combat_influence, leash_after_combat |
| 30 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | e66903252a1f5658 | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | e9b01f542c2f842b | 1 | 1 | 1 | 1 | no | combat_influence, path_interruption |
| 30 | flee | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | ed5d21cd617afdef | 1 | 1 | 1 | 1 | no | combat_flee, combat_influence, path_interruption |
| 30 | combat chase | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | ef09c976be1d3367 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, teleport_or_position_discontinuity |
| 30 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | efe92291414aa81d | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | combat chase | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | f1cc7fad54d2ec8a | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, path_interruption, teleport_or_position_discontinuity |
| 30 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | f1db7c1e462a5eb7 | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | combat chase | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | f2c9109a24df92e3 | 2 | 1 | 1 | 2 | no | combat_chase, combat_influence, path_interruption |
| 30 | combat chase | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | f60a5cb42926080a | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, path_interruption, teleport_or_position_discontinuity |
| 30 | combat chase | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | f977ee6fb9fb25ad | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, path_interruption, teleport_or_position_discontinuity |
| 30 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | f97806080504bfbb | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | fa9f8593d219a5db | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | fbceec3a13f532c9 | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | fbece79a67bfe662 | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | fe5b81d026444fb4 | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | combat chase | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | ff3c814ae34c44f0 | 2 | 1 | 1 | 2 | no | combat_chase, combat_influence, path_interruption, teleport_or_position_discontinuity |
| 30 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | 00912df00d4d3dab | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | leash | 97 | 96195 | 1 | 1044525 | Anger Manifestation | 00a29df77c508e83 | 1 | 1 | 1 | 1 | no | combat_influence, leash_after_combat |
| 30 | combat chase | 97 | 96195 | 1 | 1044525 | Anger Manifestation | 01031e51f184ebd3 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, path_interruption, teleport_or_position_discontinuity |
| 30 | combat chase | 97 | 96195 | 1 | 1044525 | Anger Manifestation | 01ca505c86d0ef9b | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, path_interruption, teleport_or_position_discontinuity |
| 30 | leash | 97 | 96195 | 1 | 1044525 | Anger Manifestation | 02ce752509ffd3c2 | 1 | 1 | 1 | 1 | no | combat_influence, leash_after_combat |
| 30 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | 03fb00db19b84330 | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | combat chase | 97 | 96195 | 1 | 1044525 | Anger Manifestation | 063e2b1dacd41ba1 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, path_interruption, teleport_or_position_discontinuity |
| 30 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | 0b1836fd8684f900 | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | combat chase | 97 | 96195 | 1 | 1044525 | Anger Manifestation | 0e88a3f8a9eb88a3 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, path_interruption, teleport_or_position_discontinuity |
| 30 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | 1279aa6fd470aec4 | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | 1339d60d23996f8a | 1 | 1 | 1 | 1 | no | path_interruption, teleport_or_position_discontinuity |
| 30 | combat chase | 97 | 96195 | 1 | 1044525 | Anger Manifestation | 15ac733c5466bb5f | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, path_interruption, teleport_or_position_discontinuity |
| 30 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | 15ae8e82f5e5f71a | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | 169b44812074d127 | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | combat chase | 97 | 96195 | 1 | 1044525 | Anger Manifestation | 19bd3aea4f998385 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, path_interruption, teleport_or_position_discontinuity |
| 30 | leash | 97 | 96195 | 1 | 1044525 | Anger Manifestation | 1f2ca75e8e714117 | 1 | 1 | 1 | 1 | no | combat_influence, leash_after_combat |
| 30 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | 204eb0925b40cb47 | 1 | 1 | 1 | 1 | no | combat_influence |
| 30 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | 20e2da4904c17ee9 | 1 | 1 | 1 | 1 | no | combat_influence |
| 30 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | 271bc4c8a40e97c8 | 1 | 1 | 1 | 1 | no | combat_influence, path_interruption |
| 30 | combat chase | 97 | 96195 | 1 | 1044525 | Anger Manifestation | 33296baf408c8b91 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, path_interruption, teleport_or_position_discontinuity |
| 30 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | 3462e196a07117fb | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | 34e128f64ae28302 | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | 387047a9185d0b7c | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | combat chase | 97 | 96195 | 1 | 1044525 | Anger Manifestation | 39cb95323a0664b5 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, path_interruption, teleport_or_position_discontinuity |
| 30 | combat chase | 97 | 96195 | 1 | 1044525 | Anger Manifestation | 3d00b6812aaada51 | 2 | 1 | 1 | 2 | no | combat_chase, combat_influence, path_interruption |
| 30 | flee | 97 | 96195 | 1 | 1044525 | Anger Manifestation | 3d82b933e41048bd | 1 | 1 | 1 | 1 | no | combat_flee, combat_influence, path_interruption |
| 30 | combat chase | 97 | 96195 | 1 | 1044525 | Anger Manifestation | 3db064b7f9037f4d | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, path_interruption |
| 30 | leash | 97 | 96195 | 1 | 1044525 | Anger Manifestation | 3dceada1ea0cf4bc | 1 | 1 | 1 | 1 | no | combat_influence, leash_after_combat, path_interruption |
| 30 | combat chase | 97 | 96195 | 1 | 1044525 | Anger Manifestation | 40213ad199f73523 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, path_interruption, teleport_or_position_discontinuity |
| 30 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | 419d6af16d6ef0e3 | 1 | 1 | 1 | 1 | no | combat_influence, path_interruption |
| 30 | leash | 97 | 96195 | 1 | 1044525 | Anger Manifestation | 41a3a68e25747d6d | 1 | 1 | 1 | 1 | no | combat_influence, leash_after_combat |
| 30 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | 443bc6d1daeebca1 | 1 | 1 | 1 | 1 | no | combat_influence, path_interruption |
| 30 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | 4531faa1f797bc39 | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | flee | 97 | 96195 | 1 | 1044525 | Anger Manifestation | 45541d52ba08697d | 1 | 1 | 1 | 1 | no | combat_flee, combat_influence, path_interruption |
| 30 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | 476b7c5cc6d44dca | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | combat chase | 97 | 96195 | 1 | 1044525 | Anger Manifestation | 4b569847405736fd | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, path_interruption, teleport_or_position_discontinuity |
| 30 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | 4d530a03833e182b | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | 4d9aa1e3265544c0 | 1 | 1 | 1 | 1 | no | combat_influence, path_interruption |
| 30 | combat chase | 97 | 96195 | 1 | 1044525 | Anger Manifestation | 4dc06e414f839b0f | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, path_interruption, teleport_or_position_discontinuity |
| 30 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | 524e92f28c800ce2 | 2 | 1 | 1 | 2 | no | path_interruption |
| 30 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | 531bdc6982a1babe | 1 | 1 | 1 | 1 | no | combat_influence, path_interruption |
| 30 | combat chase | 97 | 96195 | 1 | 1044525 | Anger Manifestation | 536a2af491ff3dda | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, path_interruption, teleport_or_position_discontinuity |
| 30 | combat chase | 97 | 96195 | 1 | 1044525 | Anger Manifestation | 53b297f75b3bb4a0 | 2 | 1 | 1 | 2 | no | combat_chase, combat_influence, path_interruption, teleport_or_position_discontinuity |
| 30 | combat chase | 97 | 96195 | 1 | 1044525 | Anger Manifestation | 56312bf92451628a | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, path_interruption, teleport_or_position_discontinuity |
| 30 | combat chase | 97 | 96195 | 1 | 1044525 | Anger Manifestation | 570f04a22776b764 | 3 | 1 | 1 | 2 | no | combat_chase, combat_influence |
| 30 | combat chase | 97 | 96195 | 1 | 1044525 | Anger Manifestation | 57472971e2f319c2 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, path_interruption |
| 30 | combat chase | 97 | 96195 | 1 | 1044525 | Anger Manifestation | 584db397dc528095 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, path_interruption |
| 30 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | 5b522a289ff48b7f | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | flee | 97 | 96195 | 1 | 1044525 | Anger Manifestation | 5dc38e23bcd4cc8c | 1 | 1 | 1 | 1 | no | combat_flee, combat_influence, path_interruption |
| 30 | combat chase | 97 | 96195 | 1 | 1044525 | Anger Manifestation | 5f6adaf799e2af51 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, path_interruption, teleport_or_position_discontinuity |
| 30 | combat chase | 97 | 96195 | 1 | 1044525 | Anger Manifestation | 656f3252c99e5a8e | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence |
| 30 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | 6b5d9f93d50a6e47 | 1 | 1 | 1 | 1 | no | combat_influence, path_interruption |
| 30 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | 6f49113a8e20513d | 1 | 1 | 1 | 1 | no | combat_influence, path_interruption |
| 30 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | 709611b3fbcbea2b | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | 72f1ce26a7178aa4 | 1 | 1 | 1 | 1 | no | combat_influence |
| 30 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | 73344ac974c5ffc7 | 2 | 1 | 1 | 2 | no | combat_influence |
| 30 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | 7467c1ade73d1f2c | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | leash | 97 | 96195 | 1 | 1044525 | Anger Manifestation | 76d7ebed18db2925 | 1 | 1 | 1 | 1 | no | combat_influence, leash_after_combat |
| 30 | leash | 97 | 96195 | 1 | 1044525 | Anger Manifestation | 7a174ed88caea3b1 | 1 | 1 | 1 | 1 | no | combat_influence, leash_after_combat |
| 30 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | 7ecbf254561ef46f | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | 7f9adab266909a98 | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | leash | 97 | 96195 | 1 | 1044525 | Anger Manifestation | 7fda7aaa81fe67b6 | 1 | 1 | 1 | 1 | no | combat_influence, leash_after_combat, path_interruption |
| 30 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | 81e72e8a6946c239 | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | 89b7087638e7034e | 1 | 1 | 1 | 1 | no | teleport_or_position_discontinuity |
| 30 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | 93a3ee8594c98db4 | 1 | 1 | 1 | 1 | no | path_interruption, teleport_or_position_discontinuity |
| 30 | combat chase | 97 | 96195 | 1 | 1044525 | Anger Manifestation | 97da9fa420c4ac52 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, path_interruption, teleport_or_position_discontinuity |
| 30 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | 98b6a554d60544bb | 1 | 1 | 1 | 1 | no | path_interruption, teleport_or_position_discontinuity |
| 30 | combat chase | 97 | 96195 | 1 | 1044525 | Anger Manifestation | 9afc6c48cea9fd79 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, path_interruption, teleport_or_position_discontinuity |
| 30 | leash | 97 | 96195 | 1 | 1044525 | Anger Manifestation | 9b89434c1c9d8ccd | 1 | 1 | 1 | 1 | no | combat_influence, leash_after_combat, path_interruption |
| 30 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | 9c3c1902d623fdc4 | 1 | 1 | 1 | 1 | no | combat_influence |
| 30 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | 9c52caa9d6f753c4 | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | 9d2263554e97310f | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | combat chase | 97 | 96195 | 1 | 1044525 | Anger Manifestation | 9fcc99a594875f98 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, path_interruption |
| 30 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | a1ff388d7332d4fe | 2 | 1 | 1 | 2 | no | combat_influence |
| 30 | combat chase | 97 | 96195 | 1 | 1044525 | Anger Manifestation | a2e7d615b3063185 | 2 | 1 | 1 | 2 | no | combat_chase, combat_influence, path_interruption |
| 30 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | a43abd89b317636a | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | a61844573051cda5 | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | a734c7c2c0c265b4 | 1 | 1 | 1 | 1 | no | path_interruption, teleport_or_position_discontinuity |
| 30 | combat chase | 97 | 96195 | 1 | 1044525 | Anger Manifestation | a7d1b51f619d4f69 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, path_interruption |
| 30 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | a7fd39e2ec1fcb9b | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | ab43769a4ab4b24f | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | ac1c797e6c028623 | 1 | 1 | 1 | 1 | no | combat_influence |
| 30 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | addb3cbaab3e05bb | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | combat chase | 97 | 96195 | 1 | 1044525 | Anger Manifestation | af73b157de4c6af0 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, path_interruption |
| 30 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | b013832020daa20a | 1 | 1 | 1 | 1 | no | combat_influence, path_interruption |
| 30 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | b513f452bce19fa4 | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | combat chase | 97 | 96195 | 1 | 1044525 | Anger Manifestation | b6e9efd6b77c24bd | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, path_interruption, teleport_or_position_discontinuity |
| 30 | combat chase | 97 | 96195 | 1 | 1044525 | Anger Manifestation | b723c34305b031d3 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence |
| 30 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | b735744ba44096e5 | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | b74d91af0df4e2f4 | 1 | 1 | 1 | 1 | no | combat_influence |
| 30 | combat chase | 97 | 96195 | 1 | 1044525 | Anger Manifestation | b92a0c1f4fe1f7c3 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence |
| 30 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | bac8345c1cc0d449 | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | bfa7b10ca6874d3c | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | leash | 97 | 96195 | 1 | 1044525 | Anger Manifestation | c005a8d7aeed3967 | 1 | 1 | 1 | 1 | no | combat_influence, leash_after_combat |
| 30 | leash | 97 | 96195 | 1 | 1044525 | Anger Manifestation | c00c12d224b7f6be | 2 | 1 | 1 | 2 | no | combat_influence, leash_after_combat, path_interruption |
| 30 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | c0abe571af339873 | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | c1498816d3eee948 | 1 | 1 | 1 | 1 | no | teleport_or_position_discontinuity |
| 30 | combat chase | 97 | 96195 | 1 | 1044525 | Anger Manifestation | c2e8139f53af867b | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, path_interruption |
| 30 | combat chase | 97 | 96195 | 1 | 1044525 | Anger Manifestation | c42cf5065346f7b0 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, path_interruption, teleport_or_position_discontinuity |
| 30 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | c4387cbfc32171a0 | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | c511da5bd4b6ec64 | 1 | 1 | 1 | 1 | no | combat_influence |
| 30 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | c657d965a01f0fed | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | c84c91bcc31cf40f | 1 | 1 | 1 | 1 | no | combat_influence, path_interruption |
| 30 | combat chase | 97 | 96195 | 1 | 1044525 | Anger Manifestation | ce0027165552fc99 | 2 | 1 | 1 | 2 | no | combat_chase, combat_influence, path_interruption, teleport_or_position_discontinuity |
| 30 | leash | 97 | 96195 | 1 | 1044525 | Anger Manifestation | cfe70e338c70797b | 1 | 1 | 1 | 1 | no | combat_influence, leash_after_combat, path_interruption |
| 30 | combat chase | 97 | 96195 | 1 | 1044525 | Anger Manifestation | d076a83b9dd454e6 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, path_interruption, teleport_or_position_discontinuity |
| 30 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | d3478deaab68902a | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | combat chase | 97 | 96195 | 1 | 1044525 | Anger Manifestation | d589ed355d9de92b | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, path_interruption, teleport_or_position_discontinuity |
| 30 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | d8a1df6e5ed60c26 | 1 | 1 | 1 | 1 | no | combat_influence, path_interruption |
| 30 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | de18209dc8211a4d | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | def44320b188b169 | 1 | 1 | 1 | 1 | no | combat_influence |
| 30 | combat chase | 97 | 96195 | 1 | 1044525 | Anger Manifestation | e603c95f27146309 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, path_interruption |
| 30 | combat chase | 97 | 96195 | 1 | 1044525 | Anger Manifestation | e6cee835164d5471 | 2 | 1 | 1 | 2 | no | combat_chase, combat_influence, path_interruption, teleport_or_position_discontinuity |
| 30 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | e8875047aeb9a490 | 1 | 1 | 1 | 1 | no | combat_influence |
| 30 | combat chase | 97 | 96195 | 1 | 1044525 | Anger Manifestation | ee9aa1cea6da916a | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, path_interruption |
| 30 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | efe50aeef4cc9b70 | 1 | 1 | 1 | 1 | no | teleport_or_position_discontinuity |
| 30 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | f1446fd4becc5cc4 | 1 | 1 | 1 | 1 | no | combat_influence, path_interruption |
| 30 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | f3f4643453941e9a | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | leash | 97 | 96195 | 1 | 1044525 | Anger Manifestation | f43e3d6444092186 | 1 | 1 | 1 | 1 | no | combat_influence, leash_after_combat, path_interruption |
| 30 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | f69074b519d61c90 | 2 | 1 | 1 | 2 | no | path_interruption |
| 30 | combat chase | 97 | 96195 | 1 | 1044525 | Anger Manifestation | f7309d51c92c4e35 | 2 | 1 | 1 | 2 | no | combat_chase, combat_influence, path_interruption, teleport_or_position_discontinuity |
| 30 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | f80a537a593662f3 | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | combat chase | 97 | 96195 | 1 | 1044525 | Anger Manifestation | f8baa9ff6a6d475f | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence |
| 30 | combat chase | 97 | 96195 | 1 | 1044525 | Anger Manifestation | f8d7281f17dde75c | 2 | 1 | 1 | 2 | no | combat_chase, combat_influence, path_interruption |
| 30 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | f8e80982221835d0 | 1 | 1 | 1 | 1 | no | combat_influence, path_interruption |
| 30 | combat chase | 97 | 96195 | 1 | 1044525 | Anger Manifestation | f9ce2a81b8b8590f | 2 | 1 | 1 | 2 | no | combat_chase, combat_influence, path_interruption |
| 30 | combat chase | 97 | 96195 | 10 | 1044525 | Pacify | 8cef5dd94e59c047 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, path_interruption, teleport_or_position_discontinuity |
| 30 | patrol | 97 | 96195 | 10 | 1044525 | Pacify | b71d1cd5245ca23a | 2 | 1 | 1 | 2 | no | combat_influence |
| 30 | combat chase | 97 | 96195 | 10 | 1044525 | Pacify | dd0a3fe0aceda178 | 2 | 1 | 1 | 2 | no | combat_chase, combat_influence, path_interruption, teleport_or_position_discontinuity |
| 30 | patrol | 98 | 96194 | 5 | 1044525 | Pacify | 150a35864b056542 | 1 | 1 | 1 | 1 | no | combat_influence, path_interruption |
| 30 | patrol | 98 | 96194 | 5 | 1044525 | Pacify | 36b2989015cc4bcd | 1 | 1 | 1 | 1 | no | combat_influence, path_interruption |
| 30 | combat chase | 98 | 96194 | 5 | 1044525 | Pacify | 6216fafeb15a78e6 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, path_interruption |
| 30 | combat chase | 98 | 96194 | 5 | 1044525 | Pacify | aef83fd7faf1a15a | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, path_interruption |
| 30 | patrol | 98 | 96194 | 5 | 1044525 | Pacify | b50301a9977278ab | 1 | 1 | 1 | 1 | no | combat_influence, path_interruption |
| 30 | patrol | 98 | 96194 | 5 | 1044525 | Pacify | cbb71980db546c60 | 2 | 1 | 1 | 2 | no | combat_influence |
| 30 | patrol | 98 | 96194 | 5 | 1044525 | Pacify | eb4992e3f15b271c | 1 | 1 | 1 | 1 | no | combat_influence, path_interruption |
| 30 | patrol | 98 | 96194 | 5 | 1044525 | Pacify | fc1b62272b6e0797 | 1 | 1 | 1 | 1 | no | combat_influence, path_interruption |
| 30 | combat chase | 103 | 203740 | 3 | 1044525 | Violent Protester | a5259d8b7e9a65c2 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, player_influence, teleport_or_position_discontinuity |
| 30 | flee | 137 | 30365 | 10 | 1044525 | Lolly the Reet | 71da33a4ea00ef4f | 1 | 1 | 1 | 1 | no | combat_flee, combat_influence, path_interruption, player_influence, teleport_or_position_discontinuity |
| 30 | scripted | 137 | 30365 | 10 | 1044525 | Lolly the Reet | bfdf1d646bec45f7 | 1 | 1 | 1 | 1 | yes | teleport_or_position_discontinuity |
| 30 | combat chase | 137 | 30365 | 10 | 1044525 | Lolly the Reet | dfb6ef69baed7d8a | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, path_interruption, player_influence, teleport_or_position_discontinuity |
| 30 | combat chase | 1019 | 17649 | 4 | 1044525 | IIV-X Advanced Docker | 6687229cec5a3c5d | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, player_influence |
| 30 | patrol | 1019 | 17649 | 4 | 1044525 | IIV-X Advanced Docker | ebd79a48ee9e76a9 | 1 | 1 | 1 | 1 | no | combat_influence, player_influence |
| 30 | combat chase | 1019 | 17714 | 2 | 1044525 | Waste Collector | 2a1d41cc1a6d8db3 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, player_influence, teleport_or_position_discontinuity |
| 30 | combat chase | 1019 | 17714 | 2 | 1044525 | Waste Collector | 44c155b600c6b52f | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, player_influence, teleport_or_position_discontinuity |
| 30 | combat chase | 1019 | 17714 | 2 | 1044525 | Waste Collector | 96ebb550416920a5 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, player_influence, teleport_or_position_discontinuity |
| 30 | combat chase | 1019 | 17714 | 2 | 1044525 | Waste Collector | a3fbb75a535c7043 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, player_influence, teleport_or_position_discontinuity |
| 30 | combat chase | 1019 | 17714 | 2 | 1044525 | Waste Collector | ba8e5ce7385e45ce | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, player_influence, teleport_or_position_discontinuity |
| 30 | combat chase | 1019 | 17714 | 2 | 1044525 | Waste Collector | c0818e04dc6131d2 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, player_influence, teleport_or_position_discontinuity |
| 30 | combat chase | 1019 | 17714 | 2 | 1044525 | Waste Collector | d991f0cec81ed834 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, player_influence, teleport_or_position_discontinuity |
| 30 | combat chase | 1019 | 17714 | 2 | 1044525 | Waste Collector | db6ec739809d7e9e | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, player_influence, teleport_or_position_discontinuity |
| 30 | combat chase | 1019 | 17714 | 4 | 1044525 | Supreme Collector of Waste | 37b95cf23ccda5aa | 2 | 1 | 1 | 2 | no | combat_chase, combat_influence, player_influence, teleport_or_position_discontinuity |
| 30 | combat chase | 1019 | 17714 | 4 | 1044525 | Supreme Collector of Waste | 8df74acbc73d8edc | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, player_influence, teleport_or_position_discontinuity |
| 30 | combat chase | 1019 | 17720 | 13 | 1044525 | Robotic Guard Dog | 165fce5c5cb39c73 | 2 | 1 | 1 | 2 | no | combat_chase, combat_influence |
| 30 | flee | 1019 | 17720 | 13 | 1044525 | Robotic Guard Dog | 16f95238ddcd8b3d | 1 | 1 | 1 | 1 | no | combat_flee, combat_influence, path_interruption, player_influence, teleport_or_position_discontinuity |
| 30 | flee | 1019 | 17720 | 13 | 1044525 | Robotic Guard Dog | 3888ffa9071c1b59 | 1 | 1 | 1 | 1 | no | combat_flee, combat_influence, path_interruption, teleport_or_position_discontinuity |
| 30 | combat chase | 1019 | 17720 | 13 | 1044525 | Robotic Guard Dog | 38aa1e5e7e66375f | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, path_interruption, player_influence, teleport_or_position_discontinuity |
| 30 | flee | 1019 | 17720 | 13 | 1044525 | Robotic Guard Dog | b37886c5286eb8de | 1 | 1 | 1 | 1 | no | combat_flee, combat_influence, path_interruption, teleport_or_position_discontinuity |
| 30 | flee | 1019 | 17720 | 13 | 1044525 | Robotic Guard Dog | b4a817260caae371 | 1 | 1 | 1 | 1 | no | combat_flee, combat_influence, path_interruption, player_influence |
| 30 | combat chase | 1019 | 17720 | 13 | 1044525 | Robotic Guard Dog | d4d9ca5ec7667150 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, player_influence, teleport_or_position_discontinuity |
| 30 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 035104a1cee194d4 | 3 | 1 | 1 | 2 | no | teleport_or_position_discontinuity |
| 30 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 4ec5ab42b6887043 | 1 | 1 | 1 | 1 | no | path_interruption |
| 30 | patrol | 1019 | 297023 | 1 | 1044525 | Cleaning Robot | 54206386ccef26d0 | 30 | 1 | 3 | 10 | no | incomplete_capture, path_interruption |
| 30 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 8faf3eaae4bed6c9 | 87 | 1 | 2 | 3 | yes | incomplete_capture, teleport_or_position_discontinuity |
| 30 | patrol | 1019 | 297023 | 1 | 1044525 | Cleaning Robot | 9c7288f33e5358f9 | 36 | 1 | 3 | 12 | no | incomplete_capture, path_interruption |
| 30 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | bba598503ab67830 | 3 | 1 | 1 | 2 | no | teleport_or_position_discontinuity |
| 30 | patrol | 1019 | 297023 | 1 | 1044525 | Cleaning Robot | cdc76af441d355e2 | 23 | 1 | 1 | 23 | yes | incomplete_capture, path_interruption |
| 30 | patrol | 1019 | 297023 | 1 | 1044525 | Cleaning Robot | cf03b0ee76fb4b3a | 9 | 1 | 3 | 3 | no | incomplete_capture |
| 30 | combat chase | 1019 | 297023 | 2 | 1044525 | Cleanmeister Intelligence Robot | 002cb668fda06953 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, player_influence, teleport_or_position_discontinuity |
| 30 | flee | 1019 | 297023 | 2 | 1044525 | Cleanmeister Intelligence Robot | 44efb66b33fc40c2 | 1 | 1 | 1 | 1 | no | combat_flee, combat_influence, path_interruption |
| 30 | flee | 1019 | 297023 | 2 | 1044525 | Cleanmeister Intelligence Robot | 4a6344172d0fd099 | 1 | 1 | 1 | 1 | no | combat_flee, combat_influence, path_interruption, player_influence |
| 30 | combat chase | 1019 | 297023 | 2 | 1044525 | Cleanmeister Intelligence Robot | d1b745ce9fec3b15 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, player_influence, teleport_or_position_discontinuity |
| 30 | combat chase | 1019 | 297023 | 2 | 1044525 | Cleanmeister Intelligence Robot | e5bad4504557a0f6 | 2 | 1 | 1 | 2 | no | combat_chase, combat_influence, player_influence |
| 25 | patrol | unresolved | unresolved | unresolved | unresolved | Malfunctioning Cleaning Robot | 2f16c61b8691ea39 | 2 | 1 | 1 | 2 | yes | metadata_missing, teleport_or_position_discontinuity |
| 25 | patrol | unresolved | unresolved | unresolved | unresolved | Malfunctioning Cleaning Robot | 379a9b24d0339be2 | 1 | 1 | 1 | 1 | yes | metadata_missing, teleport_or_position_discontinuity |
| 25 | patrol | unresolved | unresolved | unresolved | unresolved | Malfunctioning Cleaning Robot | 40a7348d85e87101 | 22 | 1 | 1 | 3 | yes | metadata_missing, teleport_or_position_discontinuity |
| 25 | patrol | unresolved | unresolved | unresolved | unresolved | Malfunctioning Cleaning Robot | 4a3f76947dc85469 | 2 | 1 | 1 | 2 | yes | metadata_missing, teleport_or_position_discontinuity |
| 25 | patrol | unresolved | unresolved | unresolved | unresolved | Malfunctioning Cleaning Robot | 4fef9c50a30e41a9 | 1 | 1 | 1 | 1 | yes | metadata_missing, teleport_or_position_discontinuity |
| 25 | patrol | unresolved | unresolved | unresolved | unresolved | Malfunctioning Cleaning Robot | 5ef1bfa6b88685f0 | 1 | 1 | 1 | 1 | yes | metadata_missing, teleport_or_position_discontinuity |
| 25 | patrol | unresolved | unresolved | unresolved | unresolved | Malfunctioning Cleaning Robot | 652003599fe2fbfa | 2 | 1 | 1 | 2 | yes | metadata_missing, teleport_or_position_discontinuity |
| 25 | patrol | unresolved | unresolved | unresolved | unresolved | Malfunctioning Cleaning Robot | 6df98caba986712f | 24 | 1 | 1 | 4 | yes | metadata_missing, teleport_or_position_discontinuity |
| 25 | patrol | unresolved | unresolved | unresolved | unresolved | Malfunctioning Cleaning Robot | 811726f2b0bd01f1 | 3 | 1 | 1 | 2 | yes | metadata_missing, teleport_or_position_discontinuity |
| 25 | patrol | unresolved | unresolved | unresolved | unresolved | Malfunctioning Cleaning Robot | 811e9c7f27b16166 | 1 | 1 | 1 | 1 | yes | metadata_missing, teleport_or_position_discontinuity |
| 25 | patrol | unresolved | unresolved | unresolved | unresolved | Malfunctioning Cleaning Robot | 83575e9da671417c | 1 | 1 | 1 | 1 | yes | metadata_missing, teleport_or_position_discontinuity |
| 25 | patrol | unresolved | unresolved | unresolved | unresolved | Malfunctioning Cleaning Robot | 965c331b846b5ecf | 2 | 1 | 1 | 2 | yes | metadata_missing, teleport_or_position_discontinuity |
| 25 | patrol | unresolved | unresolved | unresolved | unresolved | Malfunctioning Cleaning Robot | a73c22878d5ef4b9 | 2 | 1 | 1 | 2 | yes | metadata_missing, teleport_or_position_discontinuity |
| 25 | patrol | unresolved | unresolved | unresolved | unresolved | Malfunctioning Cleaning Robot | b491a2c2c0b85fe8 | 4 | 1 | 1 | 2 | yes | metadata_missing, teleport_or_position_discontinuity |
| 25 | patrol | unresolved | unresolved | unresolved | unresolved | Malfunctioning Cleaning Robot | d99e9804f1317d19 | 1 | 1 | 1 | 1 | yes | metadata_missing, teleport_or_position_discontinuity |
| 25 | patrol | unresolved | unresolved | unresolved | unresolved | Malfunctioning Cleaning Robot | f684316e9dc8eec4 | 1 | 1 | 1 | 1 | yes | metadata_missing, teleport_or_position_discontinuity |
| 25 | patrol | 0 | 26092 | 40 | 1044525 | ICC Peacekeeper | 2936f547b5bd76e0 | 4 | 1 | 4 | 1 | no | incomplete_capture, path_interruption, teleport_or_position_discontinuity |
| 25 | spawn | 0 | 26092 | 40 | 1044525 | ICC Peacekeeper | 499129f2773e87b9 | 3 | 1 | 3 | 1 | no | incomplete_capture, path_interruption, spawn_transient_not_patrol |
| 25 | spawn | 0 | 26092 | 40 | 1044525 | ICC Peacekeeper | 517af0f3318180c1 | 2 | 1 | 2 | 1 | no | incomplete_capture, path_interruption, spawn_transient_not_patrol |
| 25 | spawn | 0 | 26092 | 40 | 1044525 | ICC Peacekeeper | 678da5df4b06d946 | 3 | 1 | 3 | 1 | no | incomplete_capture, path_interruption, spawn_transient_not_patrol, teleport_or_position_discontinuity |
| 25 | spawn | 0 | 26092 | 40 | 1044525 | ICC Peacekeeper | 86ae9a6e772ead4e | 2 | 1 | 2 | 1 | no | incomplete_capture, path_interruption, spawn_transient_not_patrol |
| 25 | spawn | 0 | 26092 | 40 | 1044525 | ICC Peacekeeper | bf5d15939b945cb5 | 2 | 1 | 2 | 1 | no | incomplete_capture, path_interruption, spawn_transient_not_patrol |
| 25 | patrol | 0 | 26092 | 40 | 1044525 | ICC Peacekeeper | ddab1e1175051fe6 | 3 | 1 | 3 | 1 | no | incomplete_capture, teleport_or_position_discontinuity |
| 25 | spawn | 0 | 26092 | 40 | 1044525 | ICC Peacekeeper | e0e5ef82fa667e4e | 2 | 1 | 2 | 1 | no | incomplete_capture, path_interruption, spawn_transient_not_patrol |
| 25 | spawn | 0 | 26092 | 40 | 1044525 | ICC Peacekeeper | ff9c6c9768112c12 | 3 | 1 | 3 | 1 | no | incomplete_capture, path_interruption, spawn_transient_not_patrol |
| 25 | patrol | 25 | 17657 | 1 | 1044525 | Garbage Flea | 0582587661729464 | 2 | 1 | 2 | 1 | no | incomplete_capture |
| 25 | spawn | 25 | 17657 | 1 | 1044525 | Garbage Flea | 370af3ce4baa9a54 | 5 | 1 | 5 | 1 | no | incomplete_capture, path_interruption, spawn_transient_not_patrol |
| 25 | spawn | 25 | 17657 | 1 | 1044525 | Garbage Flea | 3726badd65a5abf0 | 2 | 1 | 2 | 1 | no | incomplete_capture, path_interruption, spawn_transient_not_patrol |
| 25 | spawn | 25 | 17657 | 1 | 1044525 | Garbage Flea | 590c1a2a705b083a | 2 | 1 | 2 | 1 | no | incomplete_capture, spawn_transient_not_patrol |
| 25 | patrol | 25 | 17657 | 1 | 1044525 | Garbage Flea | 819d1730d6ea661f | 2 | 1 | 2 | 1 | no | incomplete_capture, path_interruption |
| 25 | spawn | 25 | 17657 | 1 | 1044525 | Garbage Flea | becc4e61394c6033 | 2 | 1 | 2 | 1 | no | incomplete_capture, path_interruption, spawn_transient_not_patrol |
| 25 | spawn | 25 | 17657 | 1 | 1044525 | Garbage Flea | d7f72377c996c0a2 | 2 | 1 | 2 | 1 | no | incomplete_capture, spawn_transient_not_patrol |
| 25 | spawn | 25 | 17657 | 1 | 1044525 | Garbage Flea | f23ab151ef601ca0 | 5 | 1 | 5 | 1 | no | incomplete_capture, spawn_transient_not_patrol |
| 25 | patrol | 25 | 17657 | 2 | 1044525 | Garbage Flea | 032aa261c0ff78b7 | 4 | 1 | 2 | 2 | no | incomplete_capture, path_interruption |
| 25 | spawn | 25 | 17657 | 2 | 1044525 | Garbage Flea | 063acfd1d7bb23f8 | 5 | 1 | 4 | 1 | no | incomplete_capture, path_interruption, spawn_transient_not_patrol |
| 25 | spawn | 25 | 17657 | 2 | 1044525 | Garbage Flea | 0836fa62d2eb77f8 | 5 | 1 | 5 | 1 | no | incomplete_capture, path_interruption, spawn_transient_not_patrol |
| 25 | patrol | 25 | 17657 | 2 | 1044525 | Garbage Flea | 5a312bcdf8f5da2a | 4 | 1 | 2 | 2 | no | incomplete_capture, path_interruption |
| 25 | patrol | 25 | 17657 | 2 | 1044525 | Garbage Flea | 5c5ed69129597888 | 2 | 1 | 1 | 2 | yes | incomplete_capture |
| 25 | patrol | 25 | 17657 | 2 | 1044525 | Garbage Flea | 819d1730d6ea661f | 2 | 1 | 2 | 1 | no | incomplete_capture, path_interruption |
| 25 | spawn | 25 | 17657 | 2 | 1044525 | Garbage Flea | 8c75bccaf9ec00f4 | 2 | 1 | 2 | 1 | no | incomplete_capture, path_interruption, spawn_transient_not_patrol |
| 25 | spawn | 25 | 17657 | 2 | 1044525 | Garbage Flea | 93dc6c97bb2a9a9f | 10 | 1 | 10 | 1 | no | incomplete_capture, path_interruption, spawn_transient_not_patrol |
| 25 | spawn | 25 | 17657 | 2 | 1044525 | Garbage Flea | c0a7e3c9fbdb0460 | 3 | 1 | 3 | 1 | no | incomplete_capture, path_interruption, spawn_transient_not_patrol |
| 25 | spawn | 25 | 17657 | 2 | 1044525 | Garbage Flea | c633655b44bea054 | 3 | 1 | 3 | 1 | no | incomplete_capture, path_interruption, spawn_transient_not_patrol |
| 25 | patrol | 25 | 17657 | 2 | 1044525 | Garbage Flea | c6f14954b8e109b4 | 8 | 1 | 4 | 2 | no | incomplete_capture, path_interruption |
| 25 | spawn | 25 | 17657 | 2 | 1044525 | Garbage Flea | ef84a6133a834415 | 2 | 1 | 2 | 1 | no | incomplete_capture, path_interruption, spawn_transient_not_patrol |
| 25 | patrol | 25 | 17657 | 6 | 1044525 | Garbage Flea | 235274576d465988 | 33 | 2 | 2 | 7 | no | incomplete_capture, path_interruption |
| 25 | patrol | 25 | 17657 | 6 | 1044525 | Garbage Flea | 7418a13d9cb595b6 | 4 | 1 | 2 | 2 | no | incomplete_capture, path_interruption |
| 25 | spawn | 25 | 17657 | 6 | 1044525 | Garbage Flea | 79b9c13c2a814ade | 2 | 1 | 2 | 1 | no | incomplete_capture, path_interruption, spawn_transient_not_patrol |
| 25 | spawn | 25 | 17657 | 6 | 1044525 | Garbage Flea | e10f64d4b32e6caf | 2 | 1 | 2 | 1 | no | incomplete_capture, path_interruption, spawn_transient_not_patrol |
| 25 | spawn | 103 | 26090 | 6 | 1044525 | Janee Forejt | cb9de998eb9f1a36 | 3 | 1 | 3 | 1 | no | incomplete_capture, path_interruption, spawn_transient_not_patrol |
| 25 | spawn | 103 | 26090 | 6 | 1044525 | Janee Forejt | f3735c5a3515f5f0 | 6 | 1 | 6 | 1 | no | incomplete_capture, path_interruption, spawn_transient_not_patrol |
| 25 | spawn | 103 | 26149 | 23 | 1044525 | Janae Seaman | c13183e034bf6996 | 2 | 1 | 2 | 1 | no | incomplete_capture, path_interruption, spawn_transient_not_patrol, teleport_or_position_discontinuity |
| 25 | spawn | 103 | 26149 | 23 | 1044525 | Janae Seaman | e56e97808dc6f4be | 4 | 1 | 2 | 2 | no | incomplete_capture, path_interruption, spawn_transient_not_patrol |
| 25 | spawn | 103 | 203740 | 2 | 1044525 | Protester | 324acae6dc12b38d | 5 | 1 | 5 | 1 | no | incomplete_capture, path_interruption, spawn_transient_not_patrol, teleport_or_position_discontinuity |
| 25 | spawn | 103 | 203740 | 2 | 1044525 | Protester | 34398fdd25114783 | 3 | 1 | 3 | 1 | no | incomplete_capture, spawn_transient_not_patrol, teleport_or_position_discontinuity |
| 25 | spawn | 103 | 203740 | 2 | 1044525 | Protester | 4694145332c20349 | 3 | 1 | 3 | 1 | no | incomplete_capture, path_interruption, spawn_transient_not_patrol, teleport_or_position_discontinuity |
| 25 | spawn | 103 | 203740 | 2 | 1044525 | Protester | 570c7fa530d99019 | 2 | 1 | 2 | 1 | no | incomplete_capture, spawn_transient_not_patrol, teleport_or_position_discontinuity |
| 25 | spawn | 103 | 203740 | 2 | 1044525 | Protester | b1716bfd0581b466 | 2 | 1 | 2 | 1 | no | incomplete_capture, path_interruption, spawn_transient_not_patrol |
| 25 | spawn | 103 | 203740 | 2 | 1044525 | Protester | fe86b7f1a26c9e0a | 2 | 1 | 2 | 1 | no | incomplete_capture, path_interruption, spawn_transient_not_patrol |
| 25 | spawn | 137 | 26125 | 10 | 1044525 | Leonora Marty | 2f974e961bf3cb85 | 2 | 1 | 2 | 1 | no | incomplete_capture, path_interruption, spawn_transient_not_patrol |
| 25 | spawn | 137 | 26125 | 10 | 1044525 | Leonora Marty | 30eb61921190e7b9 | 3 | 1 | 3 | 1 | no | incomplete_capture, path_interruption, spawn_transient_not_patrol |
| 25 | spawn | 137 | 26125 | 10 | 1044525 | Leonora Marty | afd4d36d7b873436 | 2 | 1 | 2 | 1 | no | incomplete_capture, path_interruption, spawn_transient_not_patrol |
| 25 | spawn | 137 | 26125 | 10 | 1044525 | Leonora Marty | fe4db123da7c5fc9 | 2 | 1 | 2 | 1 | no | incomplete_capture, path_interruption, spawn_transient_not_patrol |
| 25 | patrol | 1019 | 17720 | 13 | 1044525 | Robotic Guard Dog | 73fb20e84d6f0db6 | 1 | 1 | 1 | 1 | yes | combat_influence, incomplete_capture, teleport_or_position_discontinuity |
| 25 | spawn | 1019 | 297023 | 1 | 1044525 | Cleaning Robot | 10c6b7047e3b2764 | 2 | 1 | 2 | 1 | no | incomplete_capture, path_interruption, spawn_transient_not_patrol |
| 25 | spawn | 1019 | 297023 | 1 | 1044525 | Cleaning Robot | 34b2312753037d39 | 3 | 1 | 3 | 1 | no | incomplete_capture, spawn_transient_not_patrol |
| 25 | spawn | 1019 | 297023 | 1 | 1044525 | Cleaning Robot | 38d4458f362d63a6 | 5 | 1 | 5 | 1 | no | incomplete_capture, path_interruption, spawn_transient_not_patrol |
| 25 | spawn | 1019 | 297023 | 1 | 1044525 | Cleaning Robot | 5e743f05d1584620 | 2 | 1 | 2 | 1 | no | incomplete_capture, path_interruption, spawn_transient_not_patrol |
| 25 | spawn | 1019 | 297023 | 1 | 1044525 | Cleaning Robot | 685c2d58a6c77699 | 5 | 1 | 4 | 1 | no | incomplete_capture, path_interruption, spawn_transient_not_patrol |
| 25 | spawn | 1019 | 297023 | 1 | 1044525 | Cleaning Robot | 6a87330f1989ce47 | 3 | 1 | 3 | 1 | no | incomplete_capture, path_interruption, spawn_transient_not_patrol |
| 25 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 7d5d2b18200e7732 | 1 | 1 | 1 | 1 | yes | incomplete_capture |
| 25 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 845979d1161727cc | 1 | 1 | 1 | 1 | yes | incomplete_capture, teleport_or_position_discontinuity |
| 25 | spawn | 1019 | 297023 | 1 | 1044525 | Cleaning Robot | 9ab50c7d2c3a7838 | 2 | 1 | 2 | 1 | no | incomplete_capture, path_interruption, spawn_transient_not_patrol |
| 25 | spawn | 1019 | 297023 | 1 | 1044525 | Cleaning Robot | abfc82d7fe4f781b | 2 | 1 | 2 | 1 | no | incomplete_capture, path_interruption, spawn_transient_not_patrol |
| 25 | spawn | 1019 | 297023 | 1 | 1044525 | Cleaning Robot | d0f5b13b95a16e35 | 2 | 1 | 2 | 1 | no | incomplete_capture, spawn_transient_not_patrol |
| 25 | spawn | 1019 | 297023 | 1 | 1044525 | Cleaning Robot | f8c5864bc1a66b6b | 5 | 1 | 5 | 1 | no | incomplete_capture, spawn_transient_not_patrol, teleport_or_position_discontinuity |
| 20 | patrol | unresolved | unresolved | unresolved | unresolved | Malfunctioning Cleaning Robot | 8419cfb2cad60906 | 78 | 1 | 2 | 2 | yes | incomplete_capture, metadata_missing, teleport_or_position_discontinuity |
| 20 | spawn | unresolved | unresolved | unresolved | unresolved | Malfunctioning Cleaning Robot | f8c1c9092fb2466b | 151 | 1 | 2 | 2 | yes | incomplete_capture, metadata_missing, spawn_transient_not_patrol, teleport_or_position_discontinuity |
| 20 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 027db5ee05b0ad6f | 17 | 1 | 1 | 6 | no | teleport_or_position_discontinuity |
| 20 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 03f7e9086f90f209 | 24 | 1 | 1 | 13 | no | teleport_or_position_discontinuity |
| 20 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 104dfabc7b07309d | 6 | 1 | 1 | 5 | no | teleport_or_position_discontinuity |
| 20 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 1611ba71c0f6b42e | 14 | 1 | 1 | 10 | no | teleport_or_position_discontinuity |
| 20 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 1b0255e53ece8af7 | 8 | 1 | 1 | 7 | no | teleport_or_position_discontinuity |
| 20 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 2f2f0bbd55a9b021 | 13 | 1 | 1 | 6 | no | teleport_or_position_discontinuity |
| 20 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 3b3c2f4a5772c1b6 | 16 | 1 | 1 | 7 | no | path_interruption |
| 20 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 3b96f44ea729376d | 8 | 1 | 1 | 6 | no | teleport_or_position_discontinuity |
| 20 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 452869501d0e60bb | 12 | 1 | 1 | 7 | no | teleport_or_position_discontinuity |
| 20 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 5690fbaa30a47c38 | 7 | 1 | 1 | 4 | no | teleport_or_position_discontinuity |
| 20 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 6094aa1681d3587a | 11 | 1 | 1 | 5 | no | teleport_or_position_discontinuity |
| 20 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 6277aa65f11dcc42 | 13 | 1 | 1 | 7 | no | teleport_or_position_discontinuity |
| 20 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 743e99ca99d43342 | 27 | 1 | 1 | 13 | no | teleport_or_position_discontinuity |
| 20 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 7c4280512579df42 | 20 | 1 | 1 | 13 | no | teleport_or_position_discontinuity |
| 20 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | a73f32a24c77f3ba | 8 | 1 | 1 | 4 | no | path_interruption |
| 20 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | b59636f10a1b3c78 | 21 | 1 | 1 | 7 | no | teleport_or_position_discontinuity |
| 20 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | b876c8f247414c09 | 24 | 1 | 1 | 11 | no | path_interruption |
| 20 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | cef3171ecd7c1759 | 12 | 1 | 1 | 8 | no | teleport_or_position_discontinuity |
| 20 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | d0660076764cab9e | 18 | 1 | 1 | 8 | no | teleport_or_position_discontinuity |
| 20 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | df3557f1a3a55bee | 17 | 1 | 1 | 9 | no | teleport_or_position_discontinuity |
| 15 | patrol | unresolved | unresolved | unresolved | unresolved | Malfunctioning Cleaning Robot | 07cc9a9c7d9816af | 6 | 1 | 1 | 4 | yes | metadata_missing, teleport_or_position_discontinuity |
| 15 | patrol | unresolved | unresolved | unresolved | unresolved | Malfunctioning Cleaning Robot | 2100675cc70566db | 15 | 1 | 1 | 5 | yes | metadata_missing, teleport_or_position_discontinuity |
| 15 | patrol | unresolved | unresolved | unresolved | unresolved | Malfunctioning Cleaning Robot | 2e2281a6c70efdc1 | 5 | 1 | 1 | 4 | yes | metadata_missing |
| 15 | patrol | unresolved | unresolved | unresolved | unresolved | Malfunctioning Cleaning Robot | 5f25758eb4a1afac | 12 | 1 | 1 | 4 | yes | metadata_missing, teleport_or_position_discontinuity |
| 15 | spawn | unresolved | unresolved | unresolved | unresolved | Malfunctioning Cleaning Robot | 5f5bfc1a378c7012 | 48 | 1 | 1 | 1 | yes | incomplete_capture, metadata_missing, spawn_transient_not_patrol, teleport_or_position_discontinuity |
| 15 | patrol | unresolved | unresolved | unresolved | unresolved | Malfunctioning Cleaning Robot | 7293e55bbd88f135 | 8 | 1 | 1 | 5 | yes | metadata_missing, teleport_or_position_discontinuity |
| 15 | patrol | unresolved | unresolved | unresolved | unresolved | Malfunctioning Cleaning Robot | 78156a5de1e8abbe | 5 | 1 | 1 | 4 | yes | metadata_missing, teleport_or_position_discontinuity |
| 15 | patrol | unresolved | unresolved | unresolved | unresolved | Malfunctioning Cleaning Robot | 7ad6a7c770661002 | 14 | 1 | 1 | 4 | yes | metadata_missing, teleport_or_position_discontinuity |
| 15 | spawn | unresolved | unresolved | unresolved | unresolved | Malfunctioning Cleaning Robot | 7ccdd7750db6bf79 | 8 | 1 | 1 | 5 | yes | metadata_missing, spawn_transient_not_patrol, teleport_or_position_discontinuity |
| 15 | patrol | unresolved | unresolved | unresolved | unresolved | Malfunctioning Cleaning Robot | 8c78a33bdd82d342 | 5 | 1 | 1 | 4 | yes | metadata_missing, teleport_or_position_discontinuity |
| 15 | patrol | unresolved | unresolved | unresolved | unresolved | Malfunctioning Cleaning Robot | 8faf3eaae4bed6c9 | 4 | 1 | 1 | 3 | yes | metadata_missing, teleport_or_position_discontinuity |
| 15 | patrol | unresolved | unresolved | unresolved | unresolved | Malfunctioning Cleaning Robot | 95c6df4166736de1 | 5 | 1 | 1 | 4 | yes | metadata_missing, teleport_or_position_discontinuity |
| 15 | patrol | unresolved | unresolved | unresolved | unresolved | Malfunctioning Cleaning Robot | 9d2031362143e398 | 7 | 1 | 1 | 6 | no | metadata_missing, teleport_or_position_discontinuity |
| 15 | patrol | unresolved | unresolved | unresolved | unresolved | Malfunctioning Cleaning Robot | 9f3fc86a4ba3cc03 | 17 | 1 | 1 | 3 | yes | metadata_missing, teleport_or_position_discontinuity |
| 15 | spawn | unresolved | unresolved | unresolved | unresolved | Malfunctioning Cleaning Robot | ae9985f9a60aae58 | 8 | 1 | 1 | 1 | yes | incomplete_capture, metadata_missing, spawn_transient_not_patrol, teleport_or_position_discontinuity |
| 15 | spawn | unresolved | unresolved | unresolved | unresolved | Malfunctioning Cleaning Robot | b4d12b4bb9e43c87 | 35 | 1 | 1 | 1 | yes | incomplete_capture, metadata_missing, spawn_transient_not_patrol, teleport_or_position_discontinuity |
| 15 | patrol | unresolved | unresolved | unresolved | unresolved | Malfunctioning Cleaning Robot | e6d910a105a72279 | 9 | 1 | 1 | 3 | yes | metadata_missing, teleport_or_position_discontinuity |
| 15 | patrol | unresolved | unresolved | unresolved | unresolved | Malfunctioning Cleaning Robot | fddf5570bff88f69 | 17 | 1 | 1 | 4 | yes | metadata_missing, teleport_or_position_discontinuity |
| 15 | patrol | unresolved | unresolved | unresolved | unresolved | Malfunctioning Cleaning Robot | ff892eb07be42731 | 9 | 1 | 1 | 3 | yes | metadata_missing, teleport_or_position_discontinuity |
| 15 | patrol | 0 | 26092 | 40 | 1044525 | ICC Peacekeeper | 4088ec5769c14373 | 7 | 1 | 1 | 7 | no | incomplete_capture, path_interruption |
| 15 | patrol | 0 | 26092 | 40 | 1044525 | ICC Peacekeeper | 45aa41e8cb80e1d6 | 5 | 1 | 1 | 5 | no | incomplete_capture, teleport_or_position_discontinuity |
| 15 | patrol | 0 | 26092 | 40 | 1044525 | ICC Peacekeeper | 51c36c04ce5584f0 | 17 | 1 | 1 | 17 | no | incomplete_capture, path_interruption |
| 15 | patrol | 0 | 26092 | 40 | 1044525 | ICC Peacekeeper | b00701e5fe703936 | 9 | 1 | 1 | 9 | no | incomplete_capture, teleport_or_position_discontinuity |
| 15 | patrol | 0 | 26092 | 40 | 1044525 | ICC Peacekeeper | c92111ed14c7ccf6 | 13 | 1 | 1 | 13 | no | incomplete_capture, path_interruption |
| 15 | patrol | 0 | 26092 | 40 | 1044525 | ICC Peacekeeper | ca156822c7ac2499 | 11 | 1 | 1 | 11 | no | incomplete_capture |
| 15 | patrol | 0 | 26092 | 40 | 1044525 | ICC Peacekeeper | d35b74599d29024e | 12 | 1 | 1 | 12 | no | incomplete_capture, path_interruption |
| 15 | patrol | 0 | 26092 | 40 | 1044525 | ICC Peacekeeper | d9c2bcaaea8c4a36 | 5 | 1 | 1 | 5 | no | incomplete_capture, path_interruption |
| 15 | patrol | 0 | 26092 | 40 | 1044525 | ICC Peacekeeper | d9e8b60fb590753a | 7 | 1 | 1 | 7 | no | incomplete_capture, teleport_or_position_discontinuity |
| 15 | patrol | 0 | 26092 | 40 | 1044525 | ICC Peacekeeper | e2e59fe9fecf5a0c | 8 | 1 | 1 | 8 | no | incomplete_capture |
| 15 | patrol | 25 | 17657 | 1 | 1044525 | Garbage Flea | 296983e887f4fb0b | 5 | 1 | 1 | 5 | no | incomplete_capture, path_interruption |
| 15 | patrol | 25 | 17657 | 1 | 1044525 | Garbage Flea | 43ff00d38a12faad | 14 | 1 | 1 | 6 | no | incomplete_capture |
| 15 | patrol | 25 | 17657 | 1 | 1044525 | Garbage Flea | 51d5c02ec872794e | 15 | 1 | 1 | 8 | no | incomplete_capture |
| 15 | patrol | 25 | 17657 | 1 | 1044525 | Garbage Flea | 5702de8fe6aff978 | 3 | 1 | 1 | 3 | no | incomplete_capture |
| 15 | patrol | 25 | 17657 | 1 | 1044525 | Garbage Flea | 93aec1c0797fd43d | 6 | 1 | 1 | 4 | yes | incomplete_capture, path_interruption |
| 15 | patrol | 25 | 17657 | 1 | 1044525 | Garbage Flea | e3d234914d49d2ee | 4 | 1 | 1 | 4 | no | incomplete_capture, path_interruption |
| 15 | patrol | 25 | 17657 | 1 | 1044525 | Garbage Flea | f958cd2862754b59 | 3 | 1 | 1 | 3 | no | incomplete_capture |
| 15 | patrol | 25 | 17657 | 2 | 1044525 | Garbage Flea | 74255cfbb50d27a6 | 3 | 1 | 1 | 3 | no | incomplete_capture, path_interruption |
| 15 | patrol | 25 | 17657 | 2 | 1044525 | Garbage Flea | a9311001eb87eed1 | 9 | 1 | 1 | 5 | no | incomplete_capture, path_interruption |
| 15 | patrol | 25 | 17657 | 2 | 1044525 | Garbage Flea | fcdd9edc3e305806 | 3 | 1 | 1 | 3 | no | incomplete_capture |
| 15 | patrol | 25 | 17657 | 5 | 1044525 | Garbage Flea | 21c1cb3e339e7a2f | 3 | 1 | 1 | 3 | no | incomplete_capture, teleport_or_position_discontinuity |
| 15 | patrol | 25 | 17657 | 5 | 1044525 | Garbage Flea | b74a518711b57ea3 | 4 | 1 | 1 | 4 | no | incomplete_capture, path_interruption |
| 15 | patrol | 25 | 17657 | 5 | 1044525 | Garbage Flea | d473263277b1a4db | 3 | 1 | 1 | 3 | no | incomplete_capture, path_interruption |
| 15 | patrol | 25 | 17657 | 6 | 1044525 | Garbage Flea | 081ef318f2ff63ab | 12 | 1 | 1 | 12 | no | incomplete_capture, teleport_or_position_discontinuity |
| 15 | patrol | 25 | 17657 | 6 | 1044525 | Garbage Flea | 21c1cb3e339e7a2f | 3 | 1 | 1 | 3 | no | incomplete_capture, path_interruption |
| 15 | patrol | 25 | 17657 | 6 | 1044525 | Garbage Flea | 264c55ff9a7f4be2 | 3 | 1 | 1 | 3 | no | incomplete_capture |
| 15 | patrol | 25 | 17657 | 6 | 1044525 | Garbage Flea | 47a5f540bf811697 | 4 | 1 | 1 | 4 | no | incomplete_capture |
| 15 | patrol | 25 | 17657 | 6 | 1044525 | Garbage Flea | f7442cee9f50c504 | 17 | 1 | 1 | 17 | no | incomplete_capture, teleport_or_position_discontinuity |
| 15 | leash | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | 69d2a17a284d62c1 | 5 | 1 | 1 | 5 | no | combat_influence, incomplete_capture, leash_after_combat, teleport_or_position_discontinuity |
| 15 | leash | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 3439f76227805455 | 5 | 1 | 1 | 5 | no | combat_influence, incomplete_capture, leash_after_combat, teleport_or_position_discontinuity |
| 15 | leash | 97 | 96195 | 1 | 1044525 | Anger Manifestation | 9ad5581137a8b0c9 | 4 | 1 | 1 | 4 | no | combat_influence, incomplete_capture, leash_after_combat, teleport_or_position_discontinuity |
| 15 | patrol | 97 | 96195 | 10 | 1044525 | Anger Manifestation | 13f023edc86388a7 | 5 | 1 | 1 | 5 | no | incomplete_capture, teleport_or_position_discontinuity |
| 15 | patrol | 98 | 96194 | 5 | 1044525 | Distracting Sphere | 2d37948225aa8a0d | 5 | 1 | 1 | 5 | no | incomplete_capture, teleport_or_position_discontinuity |
| 15 | scripted | 103 | 26090 | 6 | 1044525 | Janee Forejt | 9109181f2e5273c6 | 39 | 1 | 3 | 13 | no | incomplete_capture, path_interruption |
| 15 | scripted | 103 | 26149 | 23 | 1044525 | Janae Seaman | 0160c354b07536cf | 16 | 1 | 2 | 8 | no | incomplete_capture, path_interruption |
| 15 | scripted | 103 | 26149 | 23 | 1044525 | Janae Seaman | b881f745988c5143 | 12 | 1 | 1 | 12 | yes | incomplete_capture, teleport_or_position_discontinuity |
| 15 | scripted | 103 | 203740 | 2 | 1044525 | Protester | 3ef74d6aa7fe988e | 6 | 1 | 2 | 3 | no | incomplete_capture, path_interruption, teleport_or_position_discontinuity |
| 15 | scripted | 103 | 203740 | 2 | 1044525 | Protester | 9aaf841770a4c5f0 | 12 | 1 | 3 | 4 | no | incomplete_capture, path_interruption, teleport_or_position_discontinuity |
| 15 | scripted | 103 | 203740 | 2 | 1044525 | Protester | b1db5a40e95a12e3 | 16 | 1 | 2 | 8 | no | incomplete_capture, path_interruption |
| 15 | scripted | 103 | 203740 | 2 | 1044525 | Protester | d3f49073948272a8 | 9 | 1 | 3 | 3 | no | incomplete_capture, path_interruption, teleport_or_position_discontinuity |
| 15 | scripted | 137 | 26125 | 10 | 1044525 | Leonora Marty | be74b75053062e5d | 38 | 1 | 2 | 19 | no | incomplete_capture, path_interruption, teleport_or_position_discontinuity |
| 15 | scripted | 137 | 26125 | 10 | 1044525 | Leonora Marty | e1e2a2bbfd5c0c05 | 8 | 1 | 2 | 4 | no | incomplete_capture, path_interruption |
| 15 | scripted | 137 | 30365 | 10 | 1044525 | Lolly the Reet | d35a019761183314 | 1 | 1 | 1 | 1 | no | teleport_or_position_discontinuity |
| 15 | patrol | 1019 | 297023 | 1 | 1044525 | Cleaning Robot | 1cffe6a41e41f6b2 | 4 | 1 | 1 | 4 | no | incomplete_capture |
| 15 | patrol | 1019 | 297023 | 1 | 1044525 | Cleaning Robot | 2b3432702ce2958f | 4 | 1 | 1 | 4 | no | incomplete_capture, path_interruption |
| 15 | patrol | 1019 | 297023 | 1 | 1044525 | Cleaning Robot | 5c3a8beb14e51351 | 9 | 1 | 1 | 9 | no | incomplete_capture, path_interruption |
| 15 | patrol | 1019 | 297023 | 1 | 1044525 | Cleaning Robot | 5f389dcea6770c1c | 19 | 1 | 1 | 19 | no | incomplete_capture, path_interruption |
| 15 | patrol | 1019 | 297023 | 1 | 1044525 | Cleaning Robot | 8be20830d80cfdc2 | 9 | 1 | 1 | 9 | no | incomplete_capture |
| 15 | patrol | 1019 | 297023 | 1 | 1044525 | Cleaning Robot | 8de92349a3fe475b | 15 | 1 | 1 | 15 | no | incomplete_capture, teleport_or_position_discontinuity |
| 15 | patrol | 1019 | 297023 | 1 | 1044525 | Cleaning Robot | 9d80883ff08a20cd | 9 | 1 | 1 | 9 | no | incomplete_capture, path_interruption |
| 15 | patrol | 1019 | 297023 | 1 | 1044525 | Cleaning Robot | d154ea4799e803e9 | 3 | 1 | 1 | 3 | no | incomplete_capture, path_interruption |
| 15 | patrol | 1019 | 297023 | 1 | 1044525 | Cleaning Robot | ef5faa9b57781d29 | 3 | 1 | 1 | 3 | no | incomplete_capture, path_interruption |
| 10 | patrol | unresolved | unresolved | unresolved | unresolved | Malfunctioning Cleaning Robot | 25b7e737255154c6 | 2 | 1 | 1 | 2 | no | metadata_missing |
| 10 | patrol | unresolved | unresolved | unresolved | unresolved | Malfunctioning Cleaning Robot | 38517ef984d7423c | 3 | 1 | 1 | 2 | no | metadata_missing, path_interruption |
| 10 | patrol | unresolved | unresolved | unresolved | unresolved | Malfunctioning Cleaning Robot | 50cb788dda81c23f | 1 | 1 | 1 | 1 | no | metadata_missing, path_interruption |
| 10 | patrol | unresolved | unresolved | unresolved | unresolved | Malfunctioning Cleaning Robot | 8a1a6a83110d735b | 1 | 1 | 1 | 1 | no | metadata_missing, path_interruption |
| 10 | spawn | unresolved | unresolved | unresolved | unresolved | Malfunctioning Cleaning Robot | b3bb31bf696ad683 | 3 | 1 | 1 | 3 | yes | incomplete_capture, metadata_missing, path_interruption, spawn_transient_not_patrol |
| 10 | patrol | unresolved | unresolved | unresolved | unresolved | Malfunctioning Cleaning Robot | c3471e3db4e620de | 1 | 1 | 1 | 1 | no | metadata_missing, path_interruption |
| 10 | patrol | unresolved | unresolved | unresolved | unresolved | Malfunctioning Cleaning Robot | cf2a4c09365fdf1a | 1 | 1 | 1 | 1 | no | metadata_missing |
| 10 | patrol | unresolved | unresolved | unresolved | unresolved | Malfunctioning Cleaning Robot | e85748270060f43f | 3 | 1 | 1 | 2 | no | metadata_missing, teleport_or_position_discontinuity |
| 10 | patrol | unresolved | unresolved | unresolved | unresolved | Malfunctioning Cleaning Robot | f8b85a797d102ece | 1 | 1 | 1 | 1 | no | metadata_missing, path_interruption |
| 10 | spawn | 0 | 26092 | 40 | 1044525 | ICC Peacekeeper | 1637c035da7f21f7 | 1 | 1 | 1 | 1 | no | incomplete_capture, path_interruption, spawn_transient_not_patrol |
| 10 | spawn | 0 | 26092 | 40 | 1044525 | ICC Peacekeeper | 306abeaa86f36516 | 1 | 1 | 1 | 1 | no | incomplete_capture, path_interruption, spawn_transient_not_patrol |
| 10 | patrol | 0 | 26092 | 40 | 1044525 | unresolved | 3c9e9f596387d0a1 | 1 | 1 | 1 | 1 | no | incomplete_capture, path_interruption |
| 10 | spawn | 0 | 26092 | 40 | 1044525 | ICC Peacekeeper | 3cf2c1583ffda176 | 1 | 1 | 1 | 1 | no | incomplete_capture, path_interruption, spawn_transient_not_patrol |
| 10 | patrol | 0 | 26092 | 40 | 1044525 | ICC Peacekeeper | 50f85fdd37256d1d | 1 | 1 | 1 | 1 | no | incomplete_capture |
| 10 | patrol | 0 | 26092 | 40 | 1044525 | ICC Peacekeeper | 53467ee5573e72c9 | 1 | 1 | 1 | 1 | no | incomplete_capture, path_interruption, teleport_or_position_discontinuity |
| 10 | spawn | 0 | 26092 | 40 | 1044525 | ICC Peacekeeper | 56eef5944006b189 | 1 | 1 | 1 | 1 | no | incomplete_capture, spawn_transient_not_patrol |
| 10 | spawn | 0 | 26092 | 40 | 1044525 | ICC Peacekeeper | 5920dd461c2d942b | 1 | 1 | 1 | 1 | no | incomplete_capture, path_interruption, spawn_transient_not_patrol |
| 10 | patrol | 0 | 26092 | 40 | 1044525 | ICC Peacekeeper | 5f77f3f05b7c0cfd | 1 | 1 | 1 | 1 | no | incomplete_capture, path_interruption |
| 10 | spawn | 0 | 26092 | 40 | 1044525 | ICC Peacekeeper | 6c5f914d73369805 | 1 | 1 | 1 | 1 | no | incomplete_capture, spawn_transient_not_patrol |
| 10 | spawn | 0 | 26092 | 40 | 1044525 | ICC Peacekeeper | 75c85eb9d5d44a07 | 1 | 1 | 1 | 1 | no | incomplete_capture, spawn_transient_not_patrol |
| 10 | patrol | 0 | 26092 | 40 | 1044525 | ICC Peacekeeper | 7683eb8bc5dc6043 | 2 | 1 | 1 | 2 | no | incomplete_capture, path_interruption |
| 10 | patrol | 0 | 26092 | 40 | 1044525 | ICC Peacekeeper | 82c5994b987422cd | 1 | 1 | 1 | 1 | no | incomplete_capture, teleport_or_position_discontinuity |
| 10 | patrol | 0 | 26092 | 40 | 1044525 | ICC Peacekeeper | 879b8e99efe12de8 | 1 | 1 | 1 | 1 | no | incomplete_capture, path_interruption |
| 10 | spawn | 0 | 26092 | 40 | 1044525 | ICC Peacekeeper | 92f9f3a120f57002 | 1 | 1 | 1 | 1 | no | incomplete_capture, path_interruption, spawn_transient_not_patrol |
| 10 | spawn | 0 | 26092 | 40 | 1044525 | ICC Peacekeeper | 94d0b2afdb694607 | 1 | 1 | 1 | 1 | no | incomplete_capture, path_interruption, spawn_transient_not_patrol |
| 10 | spawn | 0 | 26092 | 40 | 1044525 | ICC Peacekeeper | 9ee2278617767c21 | 1 | 1 | 1 | 1 | no | incomplete_capture, path_interruption, spawn_transient_not_patrol |
| 10 | patrol | 0 | 26092 | 40 | 1044525 | ICC Peacekeeper | ca8897fd8e99b64a | 1 | 1 | 1 | 1 | no | incomplete_capture, path_interruption |
| 10 | spawn | 0 | 26092 | 40 | 1044525 | ICC Peacekeeper | cb32ae09fbcc12ff | 1 | 1 | 1 | 1 | no | incomplete_capture, spawn_transient_not_patrol, teleport_or_position_discontinuity |
| 10 | patrol | 0 | 26092 | 40 | 1044525 | ICC Peacekeeper | ccb37d4877bcc102 | 1 | 1 | 1 | 1 | no | incomplete_capture, teleport_or_position_discontinuity |
| 10 | spawn | 0 | 26092 | 40 | 1044525 | ICC Peacekeeper | d37d94304d8e5a2c | 1 | 1 | 1 | 1 | no | incomplete_capture, path_interruption, spawn_transient_not_patrol |
| 10 | spawn | 0 | 26092 | 40 | 1044525 | ICC Peacekeeper | d42b5ff342aca097 | 1 | 1 | 1 | 1 | no | incomplete_capture, path_interruption, spawn_transient_not_patrol |
| 10 | patrol | 0 | 26092 | 40 | 1044525 | ICC Peacekeeper | d60b41511b66b62b | 1 | 1 | 1 | 1 | no | combat_influence, incomplete_capture, path_interruption, teleport_or_position_discontinuity |
| 10 | spawn | 0 | 26092 | 40 | 1044525 | ICC Peacekeeper | d737f6af4e216e65 | 1 | 1 | 1 | 1 | no | incomplete_capture, spawn_transient_not_patrol |
| 10 | spawn | 0 | 26092 | 40 | 1044525 | ICC Peacekeeper | dfad7c8685256dff | 1 | 1 | 1 | 1 | no | incomplete_capture, path_interruption, spawn_transient_not_patrol |
| 10 | spawn | 0 | 26092 | 40 | 1044525 | ICC Peacekeeper | e793bfdf2b7294d5 | 1 | 1 | 1 | 1 | no | incomplete_capture, path_interruption, spawn_transient_not_patrol |
| 10 | spawn | 0 | 26092 | 40 | 1044525 | ICC Peacekeeper | f89a33e62230beeb | 1 | 1 | 1 | 1 | no | incomplete_capture, path_interruption, spawn_transient_not_patrol |
| 10 | patrol | 15 | 17662 | 1 | 1044525 | Minibronto | 5283b57170388664 | 1 | 1 | 1 | 1 | no | incomplete_capture |
| 10 | spawn | 15 | 17662 | 1 | 1044525 | Minibronto | b72d861b8b21dd26 | 1 | 1 | 1 | 1 | no | incomplete_capture, spawn_transient_not_patrol |
| 10 | spawn | 25 | 17657 | 1 | 1044525 | Garbage Flea | 1307cc6d8e6cb718 | 1 | 1 | 1 | 1 | no | incomplete_capture, path_interruption, spawn_transient_not_patrol |
| 10 | patrol | 25 | 17657 | 1 | 1044525 | Garbage Flea | 47e1a8d2d52c57b0 | 1 | 1 | 1 | 1 | no | incomplete_capture |
| 10 | patrol | 25 | 17657 | 1 | 1044525 | Garbage Flea | 6e1ab0d1679f0f27 | 1 | 1 | 1 | 1 | no | incomplete_capture, path_interruption |
| 10 | spawn | 25 | 17657 | 1 | 1044525 | Garbage Flea | 7794aa3e093a9956 | 1 | 1 | 1 | 1 | no | incomplete_capture, path_interruption, spawn_transient_not_patrol |
| 10 | spawn | 25 | 17657 | 1 | 1044525 | Garbage Flea | 796fe4b8ec637667 | 1 | 1 | 1 | 1 | no | incomplete_capture, spawn_transient_not_patrol |
| 10 | spawn | 25 | 17657 | 1 | 1044525 | Garbage Flea | 93dc6c97bb2a9a9f | 1 | 1 | 1 | 1 | no | incomplete_capture, path_interruption, spawn_transient_not_patrol |
| 10 | spawn | 25 | 17657 | 1 | 1044525 | Garbage Flea | 991fe68d7a8d1038 | 1 | 1 | 1 | 1 | no | incomplete_capture, spawn_transient_not_patrol |
| 10 | spawn | 25 | 17657 | 1 | 1044525 | Garbage Flea | 9bcc82edc88c9461 | 1 | 1 | 1 | 1 | no | incomplete_capture, spawn_transient_not_patrol |
| 10 | patrol | 25 | 17657 | 1 | 1044525 | Garbage Flea | c10ee996b84c3e92 | 2 | 1 | 1 | 2 | no | incomplete_capture, path_interruption |
| 10 | patrol | 25 | 17657 | 1 | 1044525 | Garbage Flea | c1422de801d5e7e9 | 1 | 1 | 1 | 1 | no | incomplete_capture, path_interruption |
| 10 | spawn | 25 | 17657 | 1 | 1044525 | Garbage Flea | d18480312668fc08 | 1 | 1 | 1 | 1 | no | incomplete_capture, spawn_transient_not_patrol |
| 10 | spawn | 25 | 17657 | 1 | 1044525 | Garbage Flea | d473bbdaa33a44ad | 1 | 1 | 1 | 1 | no | incomplete_capture, spawn_transient_not_patrol |
| 10 | spawn | 25 | 17657 | 1 | 1044525 | Garbage Flea | d9663dcd27883226 | 1 | 1 | 1 | 1 | no | incomplete_capture, path_interruption, spawn_transient_not_patrol |
| 10 | spawn | 25 | 17657 | 1 | 1044525 | Garbage Flea | e5a6dfef2fe52c2f | 1 | 1 | 1 | 1 | no | incomplete_capture, path_interruption, spawn_transient_not_patrol |
| 10 | patrol | 25 | 17657 | 1 | 1044525 | Garbage Flea | fd2558b14fdea092 | 1 | 1 | 1 | 1 | no | incomplete_capture, path_interruption |
| 10 | spawn | 25 | 17657 | 2 | 1044525 | Garbage Flea | 205b9fa0c749e6a7 | 1 | 1 | 1 | 1 | no | incomplete_capture, path_interruption, spawn_transient_not_patrol |
| 10 | spawn | 25 | 17657 | 2 | 1044525 | Garbage Flea | 452dec86aebf6c9e | 1 | 1 | 1 | 1 | no | incomplete_capture, spawn_transient_not_patrol |
| 10 | patrol | 25 | 17657 | 2 | 1044525 | Garbage Flea | 5d122b8dac32a255 | 1 | 1 | 1 | 1 | no | incomplete_capture |
| 10 | spawn | 25 | 17657 | 2 | 1044525 | Garbage Flea | 61f7a2d4e778b1be | 1 | 1 | 1 | 1 | no | incomplete_capture, spawn_transient_not_patrol |
| 10 | patrol | 25 | 17657 | 2 | 1044525 | Garbage Flea | 6b63c73bf5106da9 | 1 | 1 | 1 | 1 | no | incomplete_capture |
| 10 | spawn | 25 | 17657 | 2 | 1044525 | Garbage Flea | 6e1ab0d1679f0f27 | 1 | 1 | 1 | 1 | no | incomplete_capture, spawn_transient_not_patrol |
| 10 | spawn | 25 | 17657 | 2 | 1044525 | Garbage Flea | 6ee4ffabefc64d68 | 1 | 1 | 1 | 1 | no | incomplete_capture, path_interruption, spawn_transient_not_patrol |
| 10 | patrol | 25 | 17657 | 2 | 1044525 | Garbage Flea | 78bf4edc3167c4c2 | 1 | 1 | 1 | 1 | no | incomplete_capture |
| 10 | spawn | 25 | 17657 | 2 | 1044525 | Garbage Flea | 7ca9cc4fd76d1b30 | 1 | 1 | 1 | 1 | no | incomplete_capture, spawn_transient_not_patrol |
| 10 | spawn | 25 | 17657 | 2 | 1044525 | Garbage Flea | 9e1cfce2616cca44 | 1 | 1 | 1 | 1 | no | incomplete_capture, spawn_transient_not_patrol |
| 10 | patrol | 25 | 17657 | 2 | 1044525 | Garbage Flea | b17fa575fad67fe5 | 1 | 1 | 1 | 1 | no | incomplete_capture |
| 10 | patrol | 25 | 17657 | 2 | 1044525 | Garbage Flea | bb812c78afaa59c5 | 1 | 1 | 1 | 1 | no | incomplete_capture, path_interruption |
| 10 | spawn | 25 | 17657 | 2 | 1044525 | Garbage Flea | bc00f54892f1b481 | 1 | 1 | 1 | 1 | no | incomplete_capture, spawn_transient_not_patrol |
| 10 | spawn | 25 | 17657 | 2 | 1044525 | Garbage Flea | bf12e7482e8f45ee | 1 | 1 | 1 | 1 | no | incomplete_capture, path_interruption, spawn_transient_not_patrol |
| 10 | spawn | 25 | 17657 | 2 | 1044525 | Garbage Flea | d13127189e2b55d3 | 1 | 1 | 1 | 1 | no | incomplete_capture, spawn_transient_not_patrol |
| 10 | patrol | 25 | 17657 | 2 | 1044525 | Garbage Flea | e4dc76f091dac5dd | 1 | 1 | 1 | 1 | no | incomplete_capture, path_interruption |
| 10 | spawn | 25 | 17657 | 5 | 1044525 | Garbage Flea | 0272e9c6d4018a2d | 1 | 1 | 1 | 1 | no | incomplete_capture, spawn_transient_not_patrol |
| 10 | patrol | 25 | 17657 | 5 | 1044525 | Garbage Flea | 083bcfa973354b1a | 2 | 1 | 1 | 2 | no | incomplete_capture, teleport_or_position_discontinuity |
| 10 | patrol | 25 | 17657 | 5 | 1044525 | Garbage Flea | 1b36984728b31a72 | 1 | 1 | 1 | 1 | no | incomplete_capture |
| 10 | spawn | 25 | 17657 | 5 | 1044525 | Garbage Flea | 2856db55adeb63ab | 1 | 1 | 1 | 1 | no | incomplete_capture, path_interruption, spawn_transient_not_patrol |
| 10 | spawn | 25 | 17657 | 5 | 1044525 | Garbage Flea | 2d377ecc8d8e3881 | 1 | 1 | 1 | 1 | no | incomplete_capture, path_interruption, spawn_transient_not_patrol |
| 10 | spawn | 25 | 17657 | 5 | 1044525 | Garbage Flea | 3970234430433871 | 1 | 1 | 1 | 1 | no | incomplete_capture, spawn_transient_not_patrol |
| 10 | spawn | 25 | 17657 | 5 | 1044525 | Garbage Flea | 3e3cfeb3994b2397 | 1 | 1 | 1 | 1 | no | incomplete_capture, spawn_transient_not_patrol |
| 10 | patrol | 25 | 17657 | 5 | 1044525 | Garbage Flea | 4be2a1ff2f2ff9d9 | 1 | 1 | 1 | 1 | no | incomplete_capture |
| 10 | spawn | 25 | 17657 | 5 | 1044525 | Garbage Flea | 578c89a9c593ba67 | 1 | 1 | 1 | 1 | no | combat_influence, incomplete_capture, spawn_transient_not_patrol |
| 10 | combat chase | 25 | 17657 | 5 | 1044525 | Garbage Flea | 65e2625d73e16d88 | 2 | 1 | 1 | 2 | no | combat_chase, combat_influence, incomplete_capture, path_interruption, player_influence |
| 10 | spawn | 25 | 17657 | 5 | 1044525 | Garbage Flea | 660a6dbc8188e1a6 | 1 | 1 | 1 | 1 | no | incomplete_capture, spawn_transient_not_patrol |
| 10 | patrol | 25 | 17657 | 5 | 1044525 | Garbage Flea | 6aa5f98bf6204905 | 1 | 1 | 1 | 1 | no | incomplete_capture, path_interruption |
| 10 | patrol | 25 | 17657 | 5 | 1044525 | Garbage Flea | 705abf773fd0bf79 | 1 | 1 | 1 | 1 | no | incomplete_capture, teleport_or_position_discontinuity |
| 10 | patrol | 25 | 17657 | 5 | 1044525 | Garbage Flea | 7d0de74506b53498 | 1 | 1 | 1 | 1 | no | incomplete_capture |
| 10 | patrol | 25 | 17657 | 5 | 1044525 | Garbage Flea | 890a9a77ad4231f3 | 2 | 1 | 1 | 2 | no | incomplete_capture |
| 10 | patrol | 25 | 17657 | 5 | 1044525 | Garbage Flea | 95881d2856f87aa8 | 1 | 1 | 1 | 1 | no | incomplete_capture, teleport_or_position_discontinuity |
| 10 | patrol | 25 | 17657 | 5 | 1044525 | Garbage Flea | a2800a0ddf797e1b | 1 | 1 | 1 | 1 | no | incomplete_capture, path_interruption |
| 10 | spawn | 25 | 17657 | 5 | 1044525 | Garbage Flea | b0ca9cfc849bcd3b | 1 | 1 | 1 | 1 | no | incomplete_capture, spawn_transient_not_patrol |
| 10 | patrol | 25 | 17657 | 5 | 1044525 | Garbage Flea | b7696e6e55055eb3 | 1 | 1 | 1 | 1 | no | incomplete_capture, path_interruption |
| 10 | spawn | 25 | 17657 | 5 | 1044525 | Garbage Flea | cbdf94352f565032 | 1 | 1 | 1 | 1 | no | combat_influence, incomplete_capture, spawn_transient_not_patrol |
| 10 | leash | 25 | 17657 | 5 | 1044525 | Garbage Flea | d07be56dd32756c0 | 1 | 1 | 1 | 1 | no | combat_influence, incomplete_capture, leash_after_combat |
| 10 | patrol | 25 | 17657 | 5 | 1044525 | Garbage Flea | d7b115227428c06e | 1 | 1 | 1 | 1 | no | incomplete_capture |
| 10 | combat chase | 25 | 17657 | 5 | 1044525 | Garbage Flea | ec7bbe2d9a8ad14b | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, incomplete_capture, path_interruption |
| 10 | patrol | 25 | 17657 | 5 | 1044525 | Garbage Flea | fc7118d9b6107b84 | 1 | 1 | 1 | 1 | no | incomplete_capture |
| 10 | patrol | 25 | 17657 | 6 | 1044525 | Garbage Flea | 0272e9c6d4018a2d | 1 | 1 | 1 | 1 | no | incomplete_capture |
| 10 | spawn | 25 | 17657 | 6 | 1044525 | Garbage Flea | 1154d984e956a38f | 1 | 1 | 1 | 1 | no | incomplete_capture, path_interruption, spawn_transient_not_patrol |
| 10 | spawn | 25 | 17657 | 6 | 1044525 | Garbage Flea | 1e9669ae87ca2080 | 1 | 1 | 1 | 1 | no | incomplete_capture, spawn_transient_not_patrol |
| 10 | combat chase | 25 | 17657 | 6 | 1044525 | Garbage Flea | 3aeb29e34abadd8f | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, incomplete_capture, player_influence |
| 10 | spawn | 25 | 17657 | 6 | 1044525 | Garbage Flea | 5124db621859e859 | 1 | 1 | 1 | 1 | no | incomplete_capture, spawn_transient_not_patrol |
| 10 | patrol | 25 | 17657 | 6 | 1044525 | Garbage Flea | 640123f66eccb7e4 | 1 | 1 | 1 | 1 | no | incomplete_capture |
| 10 | patrol | 25 | 17657 | 6 | 1044525 | Garbage Flea | 6aa5f98bf6204905 | 1 | 1 | 1 | 1 | no | incomplete_capture, path_interruption |
| 10 | patrol | 25 | 17657 | 6 | 1044525 | Garbage Flea | 724ffc5bb799784e | 1 | 1 | 1 | 1 | no | incomplete_capture, path_interruption |
| 10 | combat chase | 25 | 17657 | 6 | 1044525 | Garbage Flea | 8189b5db5b76a0cf | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, incomplete_capture, player_influence |
| 10 | patrol | 25 | 17657 | 6 | 1044525 | Garbage Flea | 861c0023f83570e8 | 2 | 1 | 1 | 2 | no | incomplete_capture, path_interruption |
| 10 | spawn | 25 | 17657 | 6 | 1044525 | Garbage Flea | ac2042c3d2202c68 | 1 | 1 | 1 | 1 | no | incomplete_capture, spawn_transient_not_patrol |
| 10 | patrol | 25 | 17657 | 6 | 1044525 | Garbage Flea | b455d3992e1c8ef4 | 1 | 1 | 1 | 1 | no | incomplete_capture |
| 10 | patrol | 25 | 17657 | 6 | 1044525 | Garbage Flea | c8378ef2e75d720c | 1 | 1 | 1 | 1 | no | incomplete_capture, teleport_or_position_discontinuity |
| 10 | patrol | 25 | 17657 | 6 | 1044525 | Garbage Flea | d89a7045399c0f05 | 1 | 1 | 1 | 1 | no | combat_influence, incomplete_capture, path_interruption, player_influence |
| 10 | spawn | 25 | 17657 | 7 | 1044525 | Mutated Garbage Flea | 10f68878d73eeb07 | 1 | 1 | 1 | 1 | no | incomplete_capture, path_interruption, spawn_transient_not_patrol |
| 10 | leash | 25 | 17657 | 7 | 1044525 | Mutated Garbage Flea | 3260ff42d23a2031 | 1 | 1 | 1 | 1 | no | combat_influence, incomplete_capture, leash_after_combat, path_interruption, teleport_or_position_discontinuity |
| 10 | combat chase | 42 | 30360 | 10 | 1044525 | Angry Minibull | 0d28dfda687141ab | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, incomplete_capture |
| 10 | combat chase | 42 | 30360 | 10 | 1044525 | Angry Minibull | 40b302608c80222a | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, incomplete_capture, teleport_or_position_discontinuity |
| 10 | combat chase | 55 | 17687 | 5 | 1044525 | Rollerrat | 3202cf33e88c7d94 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, incomplete_capture |
| 10 | combat chase | 55 | 17687 | 5 | 1044525 | Rollerrat | 5bff1b29fd812d78 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, incomplete_capture, teleport_or_position_discontinuity |
| 10 | patrol | 55 | 17687 | 7 | 1044525 | Gnarl the Roller | bd5b03d3150417db | 1 | 1 | 1 | 1 | no | incomplete_capture, teleport_or_position_discontinuity |
| 10 | spawn | 55 | 17687 | 7 | 1044525 | Gnarl the Roller | ece3b23ac08f1b73 | 1 | 1 | 1 | 1 | no | incomplete_capture, spawn_transient_not_patrol, teleport_or_position_discontinuity |
| 10 | spawn | 58 | 17712 | 13 | 1044525 | Saltworm | 72656c60821b024b | 1 | 1 | 1 | 1 | no | incomplete_capture, spawn_transient_not_patrol, teleport_or_position_discontinuity |
| 10 | patrol | 58 | 17712 | 13 | 1044525 | Saltworm | 916250352b9b9584 | 1 | 1 | 1 | 1 | no | incomplete_capture, teleport_or_position_discontinuity |
| 10 | spawn | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | 15c68fabe2368d47 | 1 | 1 | 1 | 1 | no | incomplete_capture, spawn_transient_not_patrol |
| 10 | patrol | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | 5056e20dcdca1f06 | 1 | 1 | 1 | 1 | no | incomplete_capture |
| 10 | spawn | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | 5ac4075bad126566 | 1 | 1 | 1 | 1 | no | incomplete_capture, spawn_transient_not_patrol, teleport_or_position_discontinuity |
| 10 | patrol | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | a0a8d864d6ad5f65 | 1 | 1 | 1 | 1 | no | incomplete_capture, path_interruption |
| 10 | patrol | 95 | 17649 | 1 | 1044525 | Engineer Automaton I | d5797e76e63856c8 | 1 | 1 | 1 | 1 | no | incomplete_capture |
| 10 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | 11d64faf511e27ef | 1 | 1 | 1 | 1 | no | incomplete_capture |
| 10 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | c920d972c098a56d | 1 | 1 | 1 | 1 | no | incomplete_capture, path_interruption |
| 10 | patrol | 95 | 96056 | 2 | 1044525 | Bureaucrat Worker | e93207963183e911 | 1 | 1 | 1 | 1 | no | incomplete_capture, teleport_or_position_discontinuity |
| 10 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | 2128a3642ebecbef | 1 | 1 | 1 | 1 | no | incomplete_capture |
| 10 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | 56f2c2ba40cc8981 | 1 | 1 | 1 | 1 | no | incomplete_capture, path_interruption |
| 10 | spawn | 97 | 96195 | 1 | 1044525 | Anger Manifestation | 8100745cfba86fd2 | 1 | 1 | 1 | 1 | no | incomplete_capture, spawn_transient_not_patrol, teleport_or_position_discontinuity |
| 10 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | aa790b2097a11958 | 1 | 1 | 1 | 1 | no | incomplete_capture |
| 10 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | c9aa5708aaefe776 | 1 | 1 | 1 | 1 | no | incomplete_capture, teleport_or_position_discontinuity |
| 10 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | cf278ca5ea6cccc9 | 1 | 1 | 1 | 1 | no | incomplete_capture, teleport_or_position_discontinuity |
| 10 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | df93ceb95bde9dfb | 1 | 1 | 1 | 1 | no | incomplete_capture, path_interruption, teleport_or_position_discontinuity |
| 10 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | e328caee75ac3ccc | 1 | 1 | 1 | 1 | no | incomplete_capture |
| 10 | patrol | 97 | 96195 | 1 | 1044525 | Anger Manifestation | e599080130bfba37 | 1 | 1 | 1 | 1 | no | incomplete_capture, teleport_or_position_discontinuity |
| 10 | patrol | 97 | 96195 | 10 | 1044525 | Pacify | 069496e6f259571b | 1 | 1 | 1 | 1 | no | combat_influence, incomplete_capture, teleport_or_position_discontinuity |
| 10 | patrol | 98 | 96194 | 5 | 1044525 | Pacify | 95a863bb94995506 | 1 | 1 | 1 | 1 | no | combat_influence, incomplete_capture, teleport_or_position_discontinuity |
| 10 | spawn | 103 | 26090 | 6 | 1044525 | Janee Forejt | 2bac479979253a31 | 1 | 1 | 1 | 1 | no | incomplete_capture, spawn_transient_not_patrol |
| 10 | spawn | 103 | 26090 | 6 | 1044525 | Janee Forejt | 7b92ec5a55c4d00d | 1 | 1 | 1 | 1 | no | incomplete_capture, spawn_transient_not_patrol |
| 10 | spawn | 103 | 26090 | 6 | 1044525 | Janee Forejt | 80f788907255c725 | 1 | 1 | 1 | 1 | no | incomplete_capture, path_interruption, spawn_transient_not_patrol |
| 10 | spawn | 103 | 26090 | 6 | 1044525 | Janee Forejt | 951c12f5574b99ad | 1 | 1 | 1 | 1 | no | incomplete_capture, spawn_transient_not_patrol |
| 10 | spawn | 103 | 26090 | 6 | 1044525 | Janee Forejt | 9779b5a4c5112220 | 1 | 1 | 1 | 1 | no | incomplete_capture, path_interruption, spawn_transient_not_patrol |
| 10 | spawn | 103 | 26090 | 6 | 1044525 | Janee Forejt | 9a8828decacba551 | 1 | 1 | 1 | 1 | no | incomplete_capture, path_interruption, spawn_transient_not_patrol |
| 10 | scripted | 103 | 26149 | 23 | 1044525 | Janae Seaman | 0b2bb231e8506d7f | 4 | 1 | 4 | 1 | no | incomplete_capture, path_interruption |
| 10 | spawn | 103 | 26149 | 23 | 1044525 | Janae Seaman | 43eb7a4c225af5aa | 1 | 1 | 1 | 1 | no | incomplete_capture, spawn_transient_not_patrol, teleport_or_position_discontinuity |
| 10 | spawn | 103 | 26149 | 23 | 1044525 | Janae Seaman | 9563f848ea2305ef | 1 | 1 | 1 | 1 | no | incomplete_capture, spawn_transient_not_patrol |
| 10 | spawn | 103 | 26149 | 23 | 1044525 | Janae Seaman | e9a8c62417f875a7 | 1 | 1 | 1 | 1 | no | incomplete_capture, spawn_transient_not_patrol |
| 10 | scripted | 103 | 26149 | 23 | 1044525 | Janae Seaman | ff74c2d6bd669b6e | 3 | 1 | 3 | 1 | no | incomplete_capture, path_interruption |
| 10 | scripted | 103 | 203740 | 2 | 1044525 | Protester | 286129877262e46c | 2 | 1 | 2 | 1 | no | incomplete_capture, path_interruption |
| 10 | scripted | 103 | 203740 | 2 | 1044525 | Protester | 4c0bc89562b15c3c | 4 | 1 | 4 | 1 | no | incomplete_capture, path_interruption, teleport_or_position_discontinuity |
| 10 | scripted | 103 | 203740 | 2 | 1044525 | Protester | 53df7c84705dab15 | 2 | 1 | 2 | 1 | no | incomplete_capture, teleport_or_position_discontinuity |
| 10 | spawn | 103 | 203740 | 2 | 1044525 | Protester | 7afda878baa6e1e7 | 1 | 1 | 1 | 1 | no | incomplete_capture, path_interruption, spawn_transient_not_patrol |
| 10 | scripted | 103 | 203740 | 2 | 1044525 | Protester | 9b8703e7f7e89fc9 | 3 | 1 | 3 | 1 | no | incomplete_capture, teleport_or_position_discontinuity |
| 10 | scripted | 103 | 203740 | 2 | 1044525 | Protester | 9b975fe32948018d | 2 | 1 | 2 | 1 | no | incomplete_capture, path_interruption, teleport_or_position_discontinuity |
| 10 | spawn | 103 | 203740 | 2 | 1044525 | Protester | a74cd101246d5453 | 1 | 1 | 1 | 1 | no | incomplete_capture, path_interruption, spawn_transient_not_patrol, teleport_or_position_discontinuity |
| 10 | spawn | 103 | 203740 | 2 | 1044525 | Protester | af34e9e10af0d0bf | 1 | 1 | 1 | 1 | no | incomplete_capture, spawn_transient_not_patrol, teleport_or_position_discontinuity |
| 10 | spawn | 103 | 203740 | 2 | 1044525 | Protester | bcde5b6b7574c6d0 | 1 | 1 | 1 | 1 | no | incomplete_capture, path_interruption, spawn_transient_not_patrol |
| 10 | scripted | 103 | 203740 | 2 | 1044525 | Protester | d1460959c6173d8f | 2 | 1 | 2 | 1 | no | incomplete_capture, teleport_or_position_discontinuity |
| 10 | spawn | 103 | 290472 | 10 | 1044525 | Mario Carles | fdc0530d9cd8ac8f | 1 | 1 | 1 | 1 | no | incomplete_capture, spawn_transient_not_patrol |
| 10 | spawn | 137 | 26125 | 10 | 1044525 | Leonora Marty | 0222112671af9cec | 1 | 1 | 1 | 1 | no | incomplete_capture, spawn_transient_not_patrol |
| 10 | spawn | 137 | 26125 | 10 | 1044525 | Leonora Marty | 440d1a68f7a66da9 | 1 | 1 | 1 | 1 | no | incomplete_capture, path_interruption, spawn_transient_not_patrol |
| 10 | spawn | 137 | 26125 | 10 | 1044525 | Leonora Marty | 461e61cb3be5e6b8 | 1 | 1 | 1 | 1 | no | incomplete_capture, spawn_transient_not_patrol |
| 10 | spawn | 137 | 26125 | 10 | 1044525 | Leonora Marty | 46ee42272383110e | 1 | 1 | 1 | 1 | no | incomplete_capture, path_interruption, spawn_transient_not_patrol |
| 10 | spawn | 137 | 26125 | 10 | 1044525 | Leonora Marty | 6f5d7ead33a3c1fe | 1 | 1 | 1 | 1 | no | incomplete_capture, path_interruption, spawn_transient_not_patrol |
| 10 | spawn | 137 | 26125 | 10 | 1044525 | Leonora Marty | 77a0696e115508d5 | 1 | 1 | 1 | 1 | no | incomplete_capture, path_interruption, spawn_transient_not_patrol |
| 10 | spawn | 137 | 26125 | 10 | 1044525 | Leonora Marty | 8792febcaeae6fa8 | 1 | 1 | 1 | 1 | no | incomplete_capture, path_interruption, spawn_transient_not_patrol |
| 10 | spawn | 137 | 30365 | 10 | 1044525 | Lolly the Reet | 2165102015ce2031 | 1 | 1 | 1 | 1 | no | incomplete_capture, spawn_transient_not_patrol |
| 10 | spawn | 137 | 30365 | 10 | 1044525 | Lolly the Reet | 3d0c81911130e3f5 | 1 | 1 | 1 | 1 | no | incomplete_capture, spawn_transient_not_patrol, teleport_or_position_discontinuity |
| 10 | spawn | 137 | 30365 | 10 | 1044525 | Lolly the Reet | fa5560de097da560 | 1 | 1 | 1 | 1 | no | incomplete_capture, path_interruption, spawn_transient_not_patrol |
| 10 | combat chase | 1019 | 17720 | 13 | 1044525 | Robotic Guard Dog | 247c9e228e81a3f8 | 1 | 1 | 1 | 1 | no | combat_chase, combat_influence, incomplete_capture, player_influence |
| 10 | spawn | 1019 | 297023 | 1 | 1044525 | Cleaning Robot | 0152d0d15fad2fc8 | 1 | 1 | 1 | 1 | no | incomplete_capture, spawn_transient_not_patrol |
| 10 | spawn | 1019 | 297023 | 1 | 1044525 | Cleaning Robot | 097c2347242b0803 | 1 | 1 | 1 | 1 | no | incomplete_capture, spawn_transient_not_patrol |
| 10 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 0e64f67acd50eaff | 1 | 1 | 1 | 1 | no | incomplete_capture |
| 10 | patrol | 1019 | 297023 | 1 | 1044525 | Cleaning Robot | 490365cb797440bb | 2 | 1 | 1 | 2 | no | incomplete_capture, path_interruption |
| 10 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 4fc71c4df13ee1e6 | 1 | 1 | 1 | 1 | no | incomplete_capture |
| 10 | patrol | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | 7d614b00ef11935a | 1 | 1 | 1 | 1 | no | incomplete_capture, path_interruption |
| 10 | patrol | 1019 | 297023 | 1 | 1044525 | Cleaning Robot | a9db4285872aa013 | 2 | 1 | 1 | 2 | no | incomplete_capture |
| 10 | spawn | 1019 | 297023 | 1 | 1044525 | Cleaning Robot | ae2e3ab192ace3ca | 1 | 1 | 1 | 1 | no | incomplete_capture, path_interruption, spawn_transient_not_patrol |
| 10 | spawn | 1019 | 297023 | 1 | 1044525 | Malfunctioning Cleaning Robot | c281fc3a2012857a | 1 | 1 | 1 | 1 | no | incomplete_capture, spawn_transient_not_patrol |
| 10 | spawn | 1019 | 297023 | 1 | 1044525 | Cleaning Robot | e84b8d63eb5c85e8 | 1 | 1 | 1 | 1 | no | incomplete_capture, path_interruption, spawn_transient_not_patrol |
| 5 | patrol | unresolved | unresolved | unresolved | unresolved | Malfunctioning Cleaning Robot | 39b77f866bb2a1c5 | 1 | 1 | 1 | 1 | yes | incomplete_capture, metadata_missing |
| 5 | spawn | unresolved | unresolved | unresolved | unresolved | Malfunctioning Cleaning Robot | 8aa47aab737d7ee6 | 35 | 1 | 1 | 4 | yes | incomplete_capture, metadata_missing, spawn_transient_not_patrol, teleport_or_position_discontinuity |
| 5 | patrol | unresolved | unresolved | unresolved | unresolved | Malfunctioning Cleaning Robot | 96b731b204fd5ee5 | 1 | 1 | 1 | 1 | yes | incomplete_capture, metadata_missing, path_interruption |
| 5 | patrol | unresolved | unresolved | unresolved | unresolved | Malfunctioning Cleaning Robot | df660d7778b9816e | 1 | 1 | 1 | 1 | yes | incomplete_capture, metadata_missing |
| 0 | patrol | unresolved | unresolved | unresolved | unresolved | Malfunctioning Cleaning Robot | 167d60376aeb56a3 | 1 | 1 | 1 | 1 | no | incomplete_capture, metadata_missing |
| 0 | patrol | unresolved | unresolved | unresolved | unresolved | Protester | 25d9aa472e07a44d | 6 | 1 | 1 | 6 | no | incomplete_capture, metadata_missing, path_interruption |
| 0 | spawn | unresolved | unresolved | unresolved | unresolved | Cleaning Robot | 2eaba72fae72c8ff | 1 | 1 | 1 | 1 | no | incomplete_capture, metadata_missing, spawn_transient_not_patrol |
| 0 | spawn | unresolved | unresolved | unresolved | unresolved | Protester | 34398fdd25114783 | 1 | 1 | 1 | 1 | no | incomplete_capture, metadata_missing, spawn_transient_not_patrol |
| 0 | patrol | unresolved | unresolved | unresolved | unresolved | Cleaning Robot | 41199cbcf6da4676 | 1 | 1 | 1 | 1 | no | incomplete_capture, metadata_missing |
| 0 | patrol | unresolved | unresolved | unresolved | unresolved | Malfunctioning Cleaning Robot | 6139b377294120b3 | 27 | 1 | 1 | 13 | no | incomplete_capture, metadata_missing, path_interruption |
| 0 | patrol | unresolved | unresolved | unresolved | unresolved | Cleaning Robot | 6ab0993ac2666e39 | 1 | 1 | 1 | 1 | no | incomplete_capture, metadata_missing |
| 0 | patrol | unresolved | unresolved | unresolved | unresolved | Malfunctioning Cleaning Robot | 727ae6a257294f10 | 12 | 1 | 1 | 9 | no | metadata_missing |
| 0 | patrol | unresolved | unresolved | unresolved | unresolved | Protester | 91be161ebd4f463c | 10 | 1 | 1 | 10 | no | incomplete_capture, metadata_missing, path_interruption |
| 0 | patrol | unresolved | unresolved | unresolved | unresolved | Cleaning Robot | a2ea03cb9707e723 | 1 | 1 | 1 | 1 | no | incomplete_capture, metadata_missing |
| 0 | patrol | unresolved | unresolved | unresolved | unresolved | Malfunctioning Cleaning Robot | afbe0dc1cd6e4dc7 | 1 | 1 | 1 | 1 | no | incomplete_capture, metadata_missing |
| 0 | patrol | unresolved | unresolved | unresolved | unresolved | Cleaning Robot | c63a3e1d6522b1f4 | 5 | 1 | 1 | 5 | no | incomplete_capture, metadata_missing, path_interruption |
| 0 | spawn | unresolved | unresolved | unresolved | unresolved | Protester | e1dbd7c6e9a99000 | 1 | 1 | 1 | 1 | no | incomplete_capture, metadata_missing, spawn_transient_not_patrol |
| 0 | patrol | unresolved | unresolved | unresolved | unresolved | Malfunctioning Cleaning Robot | f0d4153f317fcfc3 | 7 | 1 | 1 | 5 | no | metadata_missing, teleport_or_position_discontinuity |
| 0 | patrol | 0 | 26092 | 40 | 1044525 | ICC Peacekeeper | 20c98f55c6d0fbab | 42 | 1 | 1 | 17 | no | incomplete_capture, teleport_or_position_discontinuity |
| 0 | patrol | 0 | 26092 | 40 | 1044525 | ICC Peacekeeper | 5c8da08197ebf657 | 53 | 1 | 1 | 17 | no | incomplete_capture, path_interruption |
| 0 | patrol | 0 | 26092 | 40 | 1044525 | ICC Peacekeeper | 6b513e9e5b6b0f08 | 54 | 1 | 1 | 15 | no | incomplete_capture, path_interruption |
| 0 | patrol | 0 | 26092 | 40 | 1044525 | ICC Peacekeeper | f009ddc7a79adf16 | 24 | 1 | 1 | 15 | no | incomplete_capture, path_interruption |
| 0 | patrol | 25 | 17657 | 1 | 1044525 | Garbage Flea | d758103c04d23b38 | 22 | 1 | 1 | 6 | no | incomplete_capture |
| 0 | patrol | 25 | 17657 | 1 | 1044525 | Garbage Flea | e3d96e14f7e36ddb | 195 | 1 | 1 | 9 | no | incomplete_capture, path_interruption |
| 0 | patrol | 25 | 17657 | 1 | 1044525 | Garbage Flea | f0a83b9bc5b906d3 | 33 | 1 | 1 | 5 | no | incomplete_capture, path_interruption |
| 0 | patrol | 25 | 17657 | 2 | 1044525 | Garbage Flea | 31a7aaf3d2375a0b | 7 | 1 | 1 | 5 | no | incomplete_capture, path_interruption |
| 0 | patrol | 25 | 17657 | 2 | 1044525 | Garbage Flea | 535bcd6dae808bf1 | 38 | 1 | 1 | 4 | no | incomplete_capture |
| 0 | patrol | 25 | 17657 | 2 | 1044525 | Garbage Flea | f8f7ed39523d8ecf | 14 | 1 | 1 | 6 | no | incomplete_capture |
| 0 | patrol | 25 | 17657 | 5 | 1044525 | Garbage Flea | c2225929b05ddcfe | 32 | 1 | 1 | 6 | no | incomplete_capture, path_interruption |
| 0 | patrol | 25 | 17657 | 5 | 1044525 | Garbage Flea | ececdd5fdbc66f77 | 17 | 1 | 1 | 7 | no | incomplete_capture |
| 0 | patrol | 25 | 17657 | 6 | 1044525 | Garbage Flea | 2efef29a85537e18 | 16 | 1 | 1 | 7 | no | incomplete_capture, path_interruption |
| 0 | patrol | 25 | 17657 | 6 | 1044525 | Garbage Flea | 8a49a9958f6122ed | 6 | 1 | 1 | 6 | no | incomplete_capture, path_interruption |
| 0 | scripted | 103 | 26090 | 6 | 1044525 | Janee Forejt | 21d1217f075e1afa | 19 | 1 | 1 | 17 | no | incomplete_capture |
| 0 | scripted | 103 | 26090 | 6 | 1044525 | Janee Forejt | 3ac4aa8c9af3289d | 7 | 1 | 1 | 7 | no | incomplete_capture, path_interruption |
| 0 | scripted | 103 | 26090 | 6 | 1044525 | Janee Forejt | 4e062afba0a3f29e | 1 | 1 | 1 | 1 | no | incomplete_capture |
| 0 | scripted | 103 | 26090 | 6 | 1044525 | Janee Forejt | 5228992f213db09c | 11 | 1 | 1 | 11 | no | incomplete_capture, path_interruption |
| 0 | scripted | 103 | 26090 | 6 | 1044525 | Janee Forejt | 55c429d4b554eee4 | 13 | 1 | 1 | 13 | no | incomplete_capture, path_interruption |
| 0 | scripted | 103 | 26090 | 6 | 1044525 | Janee Forejt | 79e2290f6050d364 | 13 | 1 | 1 | 13 | no | incomplete_capture, path_interruption |
| 0 | scripted | 103 | 26090 | 6 | 1044525 | Janee Forejt | 82dfcc404260f6d6 | 1 | 1 | 1 | 1 | no | incomplete_capture, path_interruption |
| 0 | scripted | 103 | 26090 | 6 | 1044525 | Janee Forejt | a5ea5d238a914bbb | 13 | 1 | 1 | 13 | no | incomplete_capture |
| 0 | scripted | 103 | 26090 | 6 | 1044525 | Janee Forejt | aa58f366737c2cc8 | 5 | 1 | 1 | 5 | no | incomplete_capture |
| 0 | scripted | 103 | 26090 | 6 | 1044525 | Janee Forejt | ab2d7b0a6b60538a | 3 | 1 | 1 | 3 | no | incomplete_capture, path_interruption |
| 0 | scripted | 103 | 26090 | 6 | 1044525 | Janee Forejt | d636d1e0369e9ad7 | 2 | 1 | 1 | 2 | no | incomplete_capture |
| 0 | scripted | 103 | 26090 | 6 | 1044525 | Janee Forejt | fccfee6cb7311be6 | 19 | 1 | 1 | 17 | no | incomplete_capture, path_interruption |
| 0 | scripted | 103 | 26149 | 23 | 1044525 | Janae Seaman | 13d05de4d99af961 | 9 | 1 | 1 | 9 | no | incomplete_capture |
| 0 | scripted | 103 | 26149 | 23 | 1044525 | Janae Seaman | 1a580c3ecfb1903c | 9 | 1 | 1 | 9 | no | incomplete_capture, teleport_or_position_discontinuity |
| 0 | scripted | 103 | 26149 | 23 | 1044525 | Janae Seaman | 4df6a84518df3502 | 4 | 1 | 1 | 4 | no | incomplete_capture |
| 0 | scripted | 103 | 26149 | 23 | 1044525 | Janae Seaman | 5de7fa6f2efe241c | 1 | 1 | 1 | 1 | no | incomplete_capture, path_interruption |
| 0 | scripted | 103 | 26149 | 23 | 1044525 | Janae Seaman | a1b22933f6a7bf31 | 8 | 1 | 1 | 8 | no | incomplete_capture, path_interruption |
| 0 | scripted | 103 | 26149 | 23 | 1044525 | Janae Seaman | c421fe3da6d02228 | 2 | 1 | 1 | 2 | no | incomplete_capture, path_interruption |
| 0 | scripted | 103 | 26149 | 23 | 1044525 | Janae Seaman | cd0d6f7d1ab5a1cb | 24 | 1 | 1 | 13 | yes | incomplete_capture, path_interruption |
| 0 | scripted | 103 | 203740 | 2 | 1044525 | Protester | 1583b3db629aaca9 | 2 | 1 | 1 | 2 | no | incomplete_capture, path_interruption |
| 0 | scripted | 103 | 203740 | 2 | 1044525 | Protester | 1e9673210939dd78 | 3 | 1 | 1 | 3 | no | incomplete_capture, teleport_or_position_discontinuity |
| 0 | scripted | 103 | 203740 | 2 | 1044525 | Protester | 2ced93f99b6bc041 | 7 | 1 | 1 | 7 | no | incomplete_capture, teleport_or_position_discontinuity |
| 0 | scripted | 103 | 203740 | 2 | 1044525 | Protester | 3d15a0e367b3e660 | 7 | 1 | 1 | 7 | no | incomplete_capture, teleport_or_position_discontinuity |
| 0 | scripted | 103 | 203740 | 2 | 1044525 | Protester | 5dd341d4f7f987ca | 1 | 1 | 1 | 1 | no | incomplete_capture, teleport_or_position_discontinuity |
| 0 | scripted | 103 | 203740 | 2 | 1044525 | Protester | 66ca3fcc3710c512 | 5 | 1 | 1 | 5 | no | incomplete_capture, teleport_or_position_discontinuity |
| 0 | scripted | 103 | 203740 | 2 | 1044525 | Protester | 6b1b98144f9cfa59 | 1 | 1 | 1 | 1 | no | incomplete_capture, teleport_or_position_discontinuity |
| 0 | scripted | 103 | 203740 | 2 | 1044525 | Protester | a0828649aaf7a9f7 | 4 | 1 | 1 | 4 | no | incomplete_capture, teleport_or_position_discontinuity |
| 0 | scripted | 103 | 203740 | 2 | 1044525 | Protester | aeb018fed475229e | 5 | 1 | 1 | 5 | no | incomplete_capture, path_interruption |
| 0 | scripted | 103 | 203740 | 2 | 1044525 | Protester | c9164392b4cbeb74 | 6 | 1 | 1 | 6 | no | incomplete_capture |
| 0 | scripted | 103 | 203740 | 2 | 1044525 | Protester | f9fd61ea36443400 | 1 | 1 | 1 | 1 | no | incomplete_capture, teleport_or_position_discontinuity |
| 0 | scripted | 103 | 290472 | 10 | 1044525 | Mario Carles | 04dd80789cb76851 | 1 | 1 | 1 | 1 | no | incomplete_capture, teleport_or_position_discontinuity |
| 0 | scripted | 103 | 290472 | 10 | 1044525 | Mario Carles | 3c0c3fb9c16d05f7 | 1 | 1 | 1 | 1 | no | incomplete_capture |
| 0 | scripted | 103 | 290472 | 10 | 1044525 | Mario Carles | 5c1d2e9b7a83411a | 1 | 1 | 1 | 1 | no | incomplete_capture, teleport_or_position_discontinuity |
| 0 | scripted | 103 | 290472 | 10 | 1044525 | Mario Carles | 74d0141317ce85f0 | 1 | 1 | 1 | 1 | no | incomplete_capture, teleport_or_position_discontinuity |
| 0 | scripted | 103 | 290472 | 10 | 1044525 | Mario Carles | fcade916c2929e22 | 3 | 1 | 1 | 3 | no | incomplete_capture, teleport_or_position_discontinuity |
| 0 | scripted | 137 | 26125 | 10 | 1044525 | Leonora Marty | 04a23cd6faa3b13e | 8 | 1 | 1 | 8 | no | incomplete_capture, path_interruption |
| 0 | scripted | 137 | 26125 | 10 | 1044525 | Leonora Marty | 05c86dc68e452733 | 14 | 1 | 1 | 14 | no | incomplete_capture, path_interruption |
| 0 | scripted | 137 | 26125 | 10 | 1044525 | Leonora Marty | 1a60f575975e906b | 1 | 1 | 1 | 1 | no | incomplete_capture |
| 0 | scripted | 137 | 26125 | 10 | 1044525 | Leonora Marty | 3be98d2ba4cff455 | 50 | 1 | 1 | 36 | no | incomplete_capture |
| 0 | scripted | 137 | 26125 | 10 | 1044525 | Leonora Marty | 3d1897ba75489614 | 26 | 1 | 1 | 26 | no | incomplete_capture, path_interruption |
| 0 | scripted | 137 | 26125 | 10 | 1044525 | Leonora Marty | 6bbb1078803f7883 | 3 | 1 | 1 | 3 | no | incomplete_capture, path_interruption |
| 0 | scripted | 137 | 26125 | 10 | 1044525 | Leonora Marty | 6c7757e5c69ccb05 | 11 | 1 | 1 | 11 | no | incomplete_capture, path_interruption |
| 0 | scripted | 137 | 26125 | 10 | 1044525 | Leonora Marty | 725c17871d541052 | 1 | 1 | 1 | 1 | no | incomplete_capture, path_interruption |
| 0 | scripted | 137 | 26125 | 10 | 1044525 | Leonora Marty | 975a91c1d054d77f | 7 | 1 | 1 | 7 | no | incomplete_capture, path_interruption |
| 0 | scripted | 137 | 26125 | 10 | 1044525 | Leonora Marty | c1abf0b70bd71c13 | 1 | 1 | 1 | 1 | no | incomplete_capture, path_interruption |
| 0 | scripted | 137 | 26125 | 10 | 1044525 | Leonora Marty | c77d069e349b13bf | 1 | 1 | 1 | 1 | no | incomplete_capture, teleport_or_position_discontinuity |
| 0 | scripted | 137 | 26125 | 10 | 1044525 | Leonora Marty | cfeba37ab429038c | 1 | 1 | 1 | 1 | no | incomplete_capture, path_interruption |
| 0 | scripted | 137 | 30365 | 10 | 1044525 | Lolly the Reet | 31717825b051877d | 2 | 1 | 1 | 2 | no | incomplete_capture, teleport_or_position_discontinuity |
| 0 | scripted | 137 | 30365 | 10 | 1044525 | Lolly the Reet | 82559ddb9d15f889 | 1 | 1 | 1 | 1 | no | incomplete_capture, teleport_or_position_discontinuity |
| 0 | scripted | 137 | 30365 | 10 | 1044525 | Lolly the Reet | 9167f7895a0fae81 | 1 | 1 | 1 | 1 | no | incomplete_capture, path_interruption |
| 0 | scripted | 137 | 30365 | 10 | 1044525 | Lolly the Reet | a002388f9f5a84f1 | 3 | 1 | 1 | 3 | no | incomplete_capture, teleport_or_position_discontinuity |
| 0 | patrol | 1019 | 297023 | 1 | 1044525 | Cleaning Robot | afed18131c73b824 | 21 | 1 | 1 | 21 | no | incomplete_capture, path_interruption |

## Decision reason accounting

| Exact reason | Affected path rows |
| --- | ---: |
| `teleport_or_position_discontinuity` | 5,563 |
| `incomplete_capture` | 3,826 |
| `spawn_transient_not_patrol` | 3,333 |
| `path_interruption` | 2,299 |
| `metadata_missing` | 997 |
| `combat_influence` | 914 |
| `route_not_repeated_end_to_end` | 660 |
| `open_route_not_closed` | 621 |
| `single_identity_generation_support` | 568 |
| `combat_chase` | 364 |
| `branched_route_requires_live_confirmation` | 325 |
| `leash_after_combat` | 152 |
| `player_influence` | 146 |
| `scripted_semantics_require_live_confirmation` | 67 |
| `combat_flee` | 34 |
| `insufficient_route_geometry` | 31 |

## Deterministic method

- Route coordinates are quantized to **0.5 m** and represented by a direction-independent set of canonical edges.
- Identical edge sets are collapsed even when observed under different runtime identities or respawn generations.
- Metadata comes only from exact identity-linked, fully consumed SCFU rows.
- Combat rows are clustered with 2s pre-roll and 5s post-roll; affected movement is rejected.
- Stop commands and external-target controls within 2.5s reject the route.
- Position discontinuities above 25m or 15m/s reject the route as teleport/discontinuity.
- Visibility/lifecycle gaps above 120s and capture-boundary traces reject the route as incomplete.
- `Safe` requires exact metadata, a clean closed unbranched patrol, repeated complete traversal, at least three canonical edges, and confidence >= 85.
- Clean but open, scripted, branched, or weakly repeated routes require live verification.
- Idle, spawn, combat chase, flee, and leash traces are not patrol candidates.

## Confidence score

The 0–100 score awards exact metadata, complete capture, clean influence history, closure, repetition, independent identities/generations, and sufficient geometry. It penalizes branching, scripted ambiguity, and single observations. Any hard rejection caps confidence below 50 and forces `Reject` regardless of score.

## Inputs

| Path | Bytes | SHA-256 |
| --- | ---: | --- |
| `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260722-152454/capture_info.json` | 9,350 | `d1286dea8646ccf8eafc5f89196fd0d3884f8071a69506312e6822d5021aff98` |
| `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260722-152454/movement-summary.json` | 482 | `f535db286ca72df3ea35ce2f8d463eb99d1a740c75af5d8c6f9913a728723d17` |
| `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260722-152454/npc-lifecycle-summary.json` | 6,887 | `96eb334841a8284e2916240f3583f50eb4d095f0604a940bf1360eba5f6eca4b` |
| `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260722-152454/movement-packets.csv` | 4,494,449 | `93be20063e8397b6f91ddd2e24135f35289fbf3500736bda4103aabee21d5dc5` |
| `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260722-152454/scfu-appearance.csv` | 4,636,245 | `4292ddff4c0cbc26c7960c26a7dbf8bbb3c9f1dc0cdb0b9cf88389f0c337e5f9` |
| `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260722-152454/enemy-combat.csv` | 3,499,753 | `53c3e0f43b1b235121ae994ee973b96ff76968b2c531ff907a5d6395ccc1716f` |
| `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260722-152454/enemy-state.csv` | 24,222,879 | `f4bf9b1a73dd7e75f1515efa029b62d38f4e6a7a2e405452a35ba14fe3f93465` |
| `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260722-152454/npc-lifecycle.csv` | 1,825,269 | `8c588d9d205b47da0dcbb550ccf72575fc8a251de92d7cc396a7ef83dbbaab87` |

## Capture validation

- Lifecycle processing allowed: `true`
- SCFU decoded/pending/errors: `2407/0/0`
- Movement decode errors: `0`
- Movement rows / usable paths: `10972/9526`
- Report schema: `1`
