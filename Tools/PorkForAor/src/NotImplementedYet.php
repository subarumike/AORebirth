<?php

declare(strict_types=1);

namespace PorkForAor;

/** Raised by an exporter whose rendering is not written yet. The runner skips it and warns once. */
final class NotImplementedYet extends \RuntimeException
{
}
