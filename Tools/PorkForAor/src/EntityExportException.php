<?php

declare(strict_types=1);

namespace PorkForAor;

/** One entity cannot be exported (for example an ambiguous nick); the other entities are not affected (section 8). */
final class EntityExportException extends \RuntimeException
{
}
