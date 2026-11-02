import { ButtonColor, ButtonSize, Card, Headline, HeadlineSize, LinkButton, Spinner } from '@kentico/xperience-admin-components';
import React, { ReactNode } from 'react';

import { formatUpdated } from './format';

export interface OverviewCardProps {
  readonly headline: string;
  /** What the card shows. The data time is appended. */
  readonly description?: string;
  readonly isLoading: boolean;
  readonly hasError: boolean;
  /** `false` when the source is not set up. Shows `notSetUpMessage` instead of the content. */
  readonly available?: boolean;
  readonly notSetUpMessage?: string;
  readonly updatedAt?: string | null;
  /** Full report link. */
  readonly reportHref?: string | null;
  readonly reportLabel?: string;
  readonly children: ReactNode;
}

/** Overview section card with its own loading, error and "not set up" state, and a link to the full report. */
export const OverviewCard = ({
  headline,
  description,
  isLoading,
  hasError,
  available = true,
  notSetUpMessage = 'Not set up in this project.',
  updatedAt,
  reportHref,
  reportLabel = 'Open report',
  children,
}: OverviewCardProps) => {
  const updated = updatedAt ? formatUpdated(updatedAt) : null;
  const text = [description, updated ? `Data from ${updated}.` : null].filter(Boolean).join(' ');

  let content: ReactNode;
  if (hasError) {
    content = <div className="SimpleStats-error">This part could not be loaded. Select Refresh to try again.</div>;
  } else if (isLoading) {
    content = (
      <div className="SimpleStats-overviewLoading">
        <Spinner />
      </div>
    );
  } else if (!available) {
    content = <div className="SimpleStats-empty">{notSetUpMessage}</div>;
  } else {
    content = children;
  }

  return (
    <Card
      headline={
        <div className="SimpleStats-tileHeader">
          <Headline size={HeadlineSize.M}>{headline}</Headline>
          {reportHref && (
            <div className="SimpleStats-tileActions">
              <LinkButton label={reportLabel} color={ButtonColor.Tertiary} size={ButtonSize.S} href={reportHref} />
            </div>
          )}
        </div>
      }
      description={text || undefined}
    >
      <div aria-busy={isLoading}>{content}</div>
    </Card>
  );
};
